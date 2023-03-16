using AcHelper;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Dreambuild.AutoCAD;
using Linq2Acad;
using System;
using System.Collections.Generic;
using System.Linq;
using ThCADCore.NTS;
using ThCADExtension;
using ThMEPEngineCore;
using ThMEPEngineCore.Command;
using ThMEPEngineCore.Engine;
using ThMEPEngineCore.Model;
using ThMEPTCH.Model;
using ThMEPTCH.TCHDrawServices;
using ThMEPWSS.Common;
using ThMEPWSS.Engine;
using ThMEPWSS.Model;
using ThMEPWSS.ViewModel;
using ThMEPEngineCore.CAD;
using ThMEPWSS.BushMarked;
using static ThMEPWSS.BushMarked.Utils;
using ThMEPWSS.CADExtensionsNs;
using Autodesk.AutoCAD.Runtime;
using ThMEPWSS.Command;
using NFox.Cad;
using ThMEPEngineCore.Config;
using ThMEPEngineCore.Algorithm;

namespace ThMEPWSS.BushMarked
{
    public class DataReadService
    {
        public DataReadService(Polyline bound)
        {
            if (bound != null)
            {
                var pl = PolyFromPoints(bound.Vertices().Cast<Point3d>().ToArray());
                //pl.Scale(pl.Centroid(), 10000);
                BOUND = pl;
            }
            SelectFrame = bound;
        }
        public Polyline BOUND { get; set; }
        public Polyline SelectFrame { get; set; }
        public void Read(bool skipWallAndBeamRead=false,bool readWall=true,bool readBeam=true)
        {
            if (!skipWallAndBeamRead)
            {
                //提取梁与剪力墙
                if (readBeam)
                    ReadBeams();
                if (readWall)
                    ReadWalls();
            }
            //提取管线
            ReadPipeLines();
        }
        public SelectedModelData SelectDataByBound()
        {
            SelectedModelData data;
            if (SelectFrame == null)
            {
                data = new SelectedModelData(ModelData.PipeLines, ModelData.ShearWalls, ModelData.Beams);
            }
            else
            {
                var pipeLines = ModelData.PipeLines == null ? new List<PipeLine>() : ModelData.PipeLines.Where(e => SelectFrame.Contains(e.Polyline.StartPoint) || SelectFrame.Intersects(e.Polyline)).ToList();
                var walls = ModelData.ShearWalls == null ? new List<ShearWall>() : ModelData.ShearWalls.Where(e => SelectFrame.Contains(e.Polyline.StartPoint) || SelectFrame.Intersects(e.Polyline)).ToList();
                var beams = ModelData.Beams == null ? new List<Beam>() : ModelData.Beams.Where(e => SelectFrame.Contains(e.Polyline.StartPoint) || SelectFrame.Intersects(e.Polyline)).ToList();
                data = new SelectedModelData(pipeLines, walls, beams);
            }
            return data;

        }
        void ReadWalls()
        {
            using (AcadDatabase adb = AcadDatabase.Active())
            {
                var allColumns = new List<Polyline>();
                var allWalls = new List<Polyline>();
                //获取相应的数据，框线内的房间，烟道井，墙，柱
                try
                {
                    var _wallColumnsEngine = new Engine.ThWallColumnsEngine();
                    _wallColumnsEngine.GetStructureInfo(BOUND, out allColumns, out allWalls);
                }
                catch { }
                ModelData.ShearWalls = new List<ShearWall>();
                ModelData.ShearWalls.AddRange(allWalls.Select(e => new ShearWall(e)));
            }
        }
        void ReadBeams()
        {
            using (AcadDatabase adb = AcadDatabase.Active())
            {
                var allBeams = new List<Polyline>();
                try
                {
                    var beamElements = new List<ThIfcBuildingElement>();
                    if (ThExtractBeamConfig.Instance.BeamEngineOption == BeamEngineOps.DB)
                    {
                        var db3Beams = ExtractDB3Beam(adb.Database);
                        beamElements = RecognizeDB3Beams(db3Beams, BOUND.Vertices());
                    }
                    else
                    {
                        var rawBeams = ExtractRawBeam(adb.Database);
                        beamElements = RecognizeRawBeams(rawBeams, BOUND.Vertices());
                    }
                    beamElements.ForEach(c =>
                    {
                        if (c.Outline != null && c.Outline is Polyline polyline)
                            allBeams.Add(polyline);
                    });
                }
                catch { }
                ModelData.Beams = new List<Beam>();
                ModelData.Beams.AddRange(allBeams.Select(e => new Beam(e)));
            }
        }

        private List<ThRawIfcBuildingElementData> ExtractDB3Beam(Database db)
        {
            var extraction = new ThDB3BeamExtractionEngine();
            extraction.Extract(db);
            return extraction.Results;
        }

        private List<ThRawIfcBuildingElementData> ExtractRawBeam(Database db)
        {
            var visitor = new ThRawBeamExtractionSecondVisitor()
            {
                LayerFilter = ThExtractBeamConfig.Instance.GetAllLayers(db).ToHashSet()
            };
            var extractor = new ThBuildingElementExtractor();
            extractor.Accept(visitor);
            extractor.Extract(db);
            extractor.ExtractFromMS(db);
            return visitor.Results;
        }

        private List<ThIfcBuildingElement> RecognizeDB3Beams(List<ThRawIfcBuildingElementData> datas, Point3dCollection pts)
        {
            // 移动到近原点位置
            var transformer = new ThMEPOriginTransformer();
            if (pts.Count > 0)
            {
                var center = pts.Envelope().CenterPoint();
                transformer = new ThMEPOriginTransformer(center);
            }
            else
            {
                transformer = new ThMEPOriginTransformer(datas.Select(o => o.Geometry).ToCollection());
            }
            var newPts = transformer.Transform(pts);
            var elements = ToBuildingElements(datas);
            var lineBeams = elements.OfType<ThIfcLineBeam>().ToList();
            lineBeams.ForEach(o => o.TransformBy(transformer.Displacement));

            var engine = new ThDB3BeamRecognitionEngine();
            engine.Recognize(lineBeams.OfType<ThIfcBuildingElement>().ToList(), newPts);

            // 恢复到原始位置
            engine.Elements.ForEach(o => transformer.Reset(o.Outline));
            return engine.Elements;
        }

        private List<ThIfcBuildingElement> RecognizeRawBeams(List<ThRawIfcBuildingElementData> datas, Point3dCollection pts)
        {
            // 移动到近原点位置
            var transformer = new ThMEPOriginTransformer();
            if (pts.Count > 0)
            {
                var center = pts.Envelope().CenterPoint();
                transformer = new ThMEPOriginTransformer(center);
            }
            else
            {
                transformer = new ThMEPOriginTransformer(datas.Select(o => o.Geometry).ToCollection());
            }
            datas.ForEach(o => transformer.Transform(o.Geometry));
            var newPts = transformer.Transform(pts);
            var engine = new ThRawBeamRecognitionEngine();
            engine.Recognize(datas, newPts);

            // 恢复到原始位置
            engine.Elements.ForEach(o => transformer.Reset(o.Outline));
            return engine.Elements;
        }

        private List<ThIfcBuildingElement> ToBuildingElements(List<ThRawIfcBuildingElementData> db3Elements)
        {
            return db3Elements
                .Select(o => ThIfcLineBeam.Create(o.Data as ThIfcBeamAnnotation))
                .OfType<ThIfcBuildingElement>()
                .ToList();
        }

        void ReadPipeLines()
        {
            using (AcadDatabase adb = AcadDatabase.Active())
            {
                var pipelines = new List<PipeLine>();
                var layernames = new List<string>() { "RAIN", "DRAI", "WSUP", "HYDT", "SPRL" };
                var entities = adb.ModelSpace.OfType<Entity>();//取出所有的元素,元素的图层名字如果符合就再取出
                var matchLayerEnts = entities.Where(e =>
                {
                    var layerName = e.Layer.ToUpper();
                    if (layerName.Contains("PIPE"))
                    {
                        foreach (var key in layernames)
                            if (layerName.Contains(key))
                                return true;
                        return false;
                    }
                    else return false;
                });
                foreach (var ent in matchLayerEnts)//符合管线的元素,如果是line,如果是polyline,如果是天正元素,
                {
                    if (ent is Line)
                        pipelines.Add(new PipeLine(PolyFromLine((Line)ent), GetTypeByLayer(ent)));
                    else if (ent is Polyline)
                        pipelines.Add(new PipeLine((Polyline)ent, GetTypeByLayer(ent)));
                    else if (isTCHElement(ent))
                    {
                        var diameterTitle = "";
                        var diameter=GetTCHPipeDiameter(ent.Id,ref diameterTitle);
                        var type = GetTypeByLayer(ent);//取出天正元素的图层类型
                        var explodeobjs = ent.ExplodeToDBObjectCollection().Cast<Entity>();
                        //
                        foreach (var obj in explodeobjs)
                        {
                            if (obj is Line)
                                pipelines.Add(new PipeLine(PolyFromLine((Line)obj), type)
                                { Diameter = diameter.ToString(), DiameterTitle = diameterTitle });//
                            else if (obj is Polyline)
                                pipelines.Add(new PipeLine((Polyline)obj, type)
                                { Diameter = diameter.ToString(), DiameterTitle = diameterTitle });
                        }
                    }
                }
                pipelines.ForEach(e => e.Polyline = GetProjectPolyline(e.Polyline));
                ModelData.PipeLines = new List<PipeLine>();
                ModelData.PipeLines.AddRange(pipelines);
            }
        }

        Polyline GetProjectPolyline(Polyline ply)
        {
            var pl = PolyFromPoints(ply.Vertices().Cast<Point3d>().Select(e => e.ToPoint2d().ToPoint3d()).ToArray());
            return pl;
        }

        double GetTCHPipeDiameter(ObjectId id, ref string title)
        {
            double dim = -1;
            title = "";
            var dxfData = GetDXFData(id);
            foreach (TypedValue tv in dxfData.AsArray())
            {
                if ((DxfCode)tv.TypeCode == (DxfCode)140)
                    dim = (double)tv.Value;
                else if ((DxfCode)tv.TypeCode == (DxfCode)301)
                    title = ((string)tv.Value).ToUpper();
                if (dim != -1 && title != "")
                    break;
            }
            return dim;
        }

        private static ResultBuffer GetDXFData(ObjectId tch)
        {
            InvokeTool.ads_name name = new InvokeTool.ads_name();
            InvokeTool.acdbGetAdsName(ref name, tch);

            ResultBuffer rb = new ResultBuffer();
            Interop.AttachUnmanagedObject(rb, InvokeTool.acdbEntGet(ref name), true);
            return rb;
        }
        PipeSystemType GetTypeByLayer(Entity ent)
        {
            var name = ent.Layer.ToUpper();
            if (name.Contains("RAIN")) return PipeSystemType.Rain;
            else if (name.Contains("DRAI")) return PipeSystemType.SewageAndWaste;
            else if (name.Contains("WSUP")) return PipeSystemType.WaterSupply;
            else if (name.Contains("HYDT")) return PipeSystemType.FireHydrant;
            else if (name.Contains("SPRL")) return PipeSystemType.Spray;
            else return PipeSystemType.Default;
        }
    }
}
