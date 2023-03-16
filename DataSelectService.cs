using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Dreambuild.AutoCAD;
using NFox.Cad;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ThCADCore.NTS;
using ThCADExtension;

namespace ThMEPWSS.BushMarked
{
    public class DataSelectService
    {

        public SelectModelData SelectModelDatas=new SelectModelData();
        
        public DataSelectService(List<PipeLine> pipeLines,List<Beam> beams,List<ShearWall> shearWalls)
            
        {
            SelectModelDatas.selectPipeLines = pipeLines;
            SelectModelDatas.Beams = beams;
            SelectModelDatas.ShearWalls = shearWalls;
        }

        PipeSystemType ConvertTypeNameToPipeSystemType(string s)
        {
            if (s == "污废水")
                return PipeSystemType.SewageAndWaste;
            else if (s == "雨水")
                return PipeSystemType.Rain;
            else if (s == "给水")
                return PipeSystemType.WaterSupply;
            else if (s == "消火栓")
                return PipeSystemType.FireHydrant;
            else if (s == "喷淋")
                return PipeSystemType.Spray;
            else
                return PipeSystemType.Default;  
        }
        public void SelectPipeLinesType(List<string> input)
        {
            List<PipeLine> newList = new List<PipeLine>();
            foreach (var S in input)
            {
                var type = ConvertTypeNameToPipeSystemType(S);
                foreach (var PL in SelectModelDatas.selectPipeLines)
                {
                    if (PL.PipeSystem == type)
                    {
                        newList.Add(PL);
                    }
                }
            }
            SelectModelDatas.selectPipeLines = newList;
        }
        public void SelectLeaveHoles(List<string> input)
        {
            List<Beam> BeamList=new List<Beam>();
            List<ShearWall> shearWallsList = new List<ShearWall>();
            if (input.Count == 1)
            {
                if (input[0] == "剪力墙")
                {
                    SelectModelDatas.Beams.Clear();
                }
                else
                {
                    SelectModelDatas.ShearWalls.Clear();
                }
            }
            else if(input.Count==0)
            {
                SelectModelDatas.ShearWalls.Clear();
                SelectModelDatas.Beams.Clear();
            }
        }
        public void Select(List<string> inputPipeSystemtypes,List<string> inputLeaveHoleStruct,bool selectWalls=true)
        {
            SelectPipeLinesType(inputPipeSystemtypes);
            if(selectWalls)
                SelectLeaveHoles(inputLeaveHoleStruct);
        }
        public static double GetAngle(Polyline polyline, Point3d InterSectPt,ref Line seg)
        {
            List<Line> lines = new List<Line>();
            for (int i = 1; i < polyline.NumberOfVertices; i++)
            {
                var line = new Line(polyline.GetPoint3dAt(i), polyline.GetPoint3dAt(i - 1));
                lines.Add(line);
            }
            foreach (Line L in lines)
            {
                if (L.DistanceTo(InterSectPt, false) <1)
                {
                    seg = L;
                    return L.Angle;
                    break;
                }

            }
            return 0;


        }
        public struct AngelOrUptext
        {
            AngelOrUptext(Line line,double _angle = 0, string _UpTxt = "XX")
            {
                angle = _angle;
                UpTxt = _UpTxt;
                AppendPipeLine = line;
            }
            public double angle;
            public string UpTxt;
            public Line AppendPipeLine;
        }

        public static Dictionary<Point3d, AngelOrUptext> GenerateIntersectPointAndAngel(List<PipeLine> pipeLines, List<Beam> beams, List<ShearWall> shearWalls)
        {
            //List<BushProperty> bushProperties = new List<BushProperty>();
            Dictionary<Point3d, AngelOrUptext> keyValuePairs= new Dictionary<Point3d, AngelOrUptext>();
            var objcollection = shearWalls.Select(e => e.Polyline).ToCollection();
            foreach (Beam beam in beams)
                objcollection.Add(beam.Polyline);
            ThCADCoreNTSSpatialIndex spatialIndex = new ThCADCoreNTSSpatialIndex(objcollection);
            foreach (PipeLine pipeLine in pipeLines)
            {
                var objs = spatialIndex.SelectCrossingPolygon(pipeLine.Polyline).Cast<Polyline>().ToList();
                objs.AddRange(spatialIndex.SelectFence(pipeLine.Polyline).Cast<Polyline>());
                foreach (var obj in objs)
                {   
                    var points = obj.Intersect(pipeLine.Polyline, Intersect.OnBothOperands);
                    AngelOrUptext anOUt=new AngelOrUptext();
                    if (points!=null)
                    {
                        if (points.Count > 1)
                        {
                            double tol = 601;//去重
                            for (int i = 0; i < points.Count-1; i++)
                            {
                                for (int j = i + 1; j < points.Count; j++)
                                {
                                    if (points[i].DistanceTo(points[j]) <= tol)
                                    {
                                        points[i]= new Point3d((points[i].X + points[j].X) / 2, (points[i].Y + points[j].Y) / 2,0);
                                        points.RemoveAt(j);
                                        j--;
                                    }
                                }
                            }
                        }
                        foreach (var p in points)
                        {
                            var seg = new Line();
                            anOUt.angle = GetAngle(pipeLine.Polyline, p,ref seg);
                            anOUt.UpTxt = pipeLine .DiameterTitle+ pipeLine.Diameter;
                            anOUt.AppendPipeLine = seg;
                            keyValuePairs.Add(p, anOUt);
                        }
                    }    
                }
            }
            return keyValuePairs;
        }

        public static List<Point3d> GenerateIntersectPoint(List<PipeLine> pipeLines ,List<Beam> beams,List<ShearWall> shearWalls)
        {

            List<Point3d> intersectPoints = new List<Point3d>();
            var objcollection = shearWalls.Select(e => e.Polyline).ToCollection();
            foreach (Beam beam in beams)
                objcollection.Add(beam.Polyline);
            ThCADCoreNTSSpatialIndex spatialIndex = new ThCADCoreNTSSpatialIndex(objcollection);
            foreach (PipeLine pipeLine in pipeLines)
            {
                var objs = spatialIndex.SelectCrossingPolygon(pipeLine.Polyline).Cast<Polyline>().ToList();
                objs.AddRange(spatialIndex.SelectFence(pipeLine.Polyline).Cast<Polyline>());
                foreach (var obj in objs)
                intersectPoints.AddRange(obj.Intersect(pipeLine.Polyline, Intersect.OnBothOperands));
            }
            return intersectPoints;
        }
    }
}
