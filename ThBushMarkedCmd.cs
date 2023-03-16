using AcHelper;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using DotNetARX;
using Dreambuild.AutoCAD;
using GeometryExtensions;
using Linq2Acad;
using NFox.Cad;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using ThCADCore.NTS;
using ThCADExtension;
using ThMEPEngineCore;
using ThMEPEngineCore.CAD;
using ThMEPEngineCore.Command;
using ThMEPTCH.Model;
using ThMEPTCH.TCHDrawServices;
using ThMEPWSS.BushMarked;
using ThMEPWSS.JsonExtensionsNs;
using ThMEPWSS.Uitl.ExtensionsNs;
using ThMEPWSS.ViewModel;
using static ThMEPWSS.BushMarked.DataSelectService;
using static ThMEPWSS.BushMarked.Utils;
using Line = Autodesk.AutoCAD.DatabaseServices.Line;

namespace ThMEPWSS.Command
{
    public class ThBushMarkedCmd : ThMEPBaseCommand, IDisposable
    {

        public enum CMDMode
        {
            SINGLE,//单点生成
            BATCH//批量生成kkk
        }
        public ThBushMarkedCmd(BushMarkedViewModel viewModel, CMDMode generationMode)
        {
            CommandName = "THTGBZ";
            ActionName = "套管标注";
            ViewModel = viewModel;
            GenerationMode = generationMode;
        }
        public BushMarkedViewModel ViewModel { get; set; }
        public CMDMode GenerationMode { get; set; }
        public SelectModelData SelectedData { get; set; }
        public List<Mark> Marks = new List<Mark>();

        public void Dispose() { }
        public override void SubExecute()
        {
            using (var Doclock = Active.Document.LockDocument())
            using (AcadDatabase adb = AcadDatabase.Active())
            {
                try
                {
                    ImportService importService = new ImportService();
                    importService.Import();
                    if (GenerationMode == CMDMode.SINGLE)
                    {
                        GenerateSingleBush();
                    }
                    else
                    {
                        GenerateBushesInBatches();
                    }
                }
                catch (Exception ex)
                {
                    ;
                }
            }
        }

        public static MLeader DrawMLeader(string content, Point3d p1, Point3d p2,string layoutFactor)
        {
            double textHeight = 350;
            if (layoutFactor == "1:50")
                textHeight = textHeight / 2;
            else if(layoutFactor == "1:150")
                textHeight = 450;

            var e = new MLeader();
            e.ContentType = ContentType.MTextContent;
            e.MText = new MText() { Contents = content, TextHeight = textHeight, LineSpaceDistance= textHeight+100, Rotation=0};
            e.TextStyleId = DbHelper.GetTextStyleId("TH-STYLE3");
            e.ArrowSize = 0;
            e.DoglegLength = 0;
            e.LandingGap = 0;
            e.ExtendLeaderToText = false;
            e.SetTextAttachmentType(TextAttachmentType.AttachmentBottomOfTopLine, LeaderDirectionType.LeftLeader);
            e.SetTextAttachmentType(TextAttachmentType.AttachmentBottomOfTopLine, LeaderDirectionType.RightLeader);
            e.AddLeaderLine(p1);
            
            var bd = e.MText.Bounds.ToGRect();
            var matu2w = Active.Editor.UCS2WCS();
            var matw2u = Active.Editor.WCS2UCS();
            var p3 = p2.TransformBy(matw2u).OffsetY(/*bd.Height +*/ textHeight);
            if (p2.TransformBy(matw2u).X < p1.TransformBy(matw2u).X)
            {
                p3 = p3.OffsetX(-bd.Width);
            }
            p3 = p3.TransformBy(matu2w);

            e.MText.Rotation = 0;
            e.TextLocation = p3;
            e.Layer = "W-BUSH-NOTE";
            e.AddToCurrentSpace();
            return e;
        }
        List<string> GetBushProperties()
        {
            var result = new List<string>();
            switch (ViewModel.BushTypesSelectItem)
            {
                case "普通刚套管":
                    result.Add("普通套管");
                    result.Add("");
                    break;
                case "B型刚性防水套管":
                    result.Add("B型刚性防水套管/A型防护密闭套管");
                    result.Add("BG-");
                    break;
                case "A型柔性防水套管":
                    result.Add("A型柔性防水套管");
                    result.Add("AR-");
                    break;
                case "A型防护密闭套管":
                    result.Add("B型刚性防水套管/A型防护密闭套管");
                    result.Add("AF-");
                    break;
                case "C型防护密闭套管":
                    result.Add("C型防护密闭套管（单侧挡板）");
                    result.Add("CF-");
                    break;
                case "E型防护密闭套管":
                    result.Add("E型防护密闭套管（双侧挡板）");
                    result.Add("EF-");
                    break;
                default:
                    result.Add("普通套管");
                    result.Add("");
                    break;
            }
            return result;
        }
        List<Polyline> TransferToPolyLine(List<PipeLine> pipeLines, ref List<string> diameters, ref List<string> diameters_titles)
        {
            var objcollection = pipeLines.Select(e => e.Polyline).ToList();
            diameters = pipeLines.Select(e => e.Diameter).ToList();
            diameters_titles = pipeLines.Select(e => e.DiameterTitle).ToList();
            return objcollection;

        }

        void GenerateSingleBush()
        {
            using (AcadDatabase adb = AcadDatabase.Active())
            {
                var pipe = ModelData.PipeLines[ModelData.PipeIndex];
                var bushProperties = GetBushProperties();
                var appendSeg = new Line();
                double aimAngle = DataSelectService.GetAngle(pipe.Polyline, ViewModel.FirstInsertPt,ref appendSeg);
                var blkId = adb.CurrentSpace.ObjectId.InsertBlockReference("0", "套管", ViewModel.FirstInsertPt, new Scale3d(0), Math.Abs(aimAngle - Math.PI / 2));
                var br = adb.Element<BlockReference>(blkId);

                var brAngle = aimAngle + Math.PI / 2;
                var brExtSeg = br.GeometricExtents.ToRectangle().GetEdges().First();
                var v1 = PressureDrainageSystem.Utils.PressureDrainageUtils.CreateVector(appendSeg);
                var v2 = PressureDrainageSystem.Utils.PressureDrainageUtils.CreateVector(brExtSeg);
                if (!(IsParallelVector(v1, v2) || IsPerpVector(v1, v2)))
                {
                    br.Rotation = brAngle;
                }
                blkId.SetDynBlockValue("可见性", bushProperties[0]);
                br.Layer = "W-BUSH";
                var insertPt = ((Extents3d)br.Bounds).CenterPoint();
                br.TransformBy(Matrix3d.Displacement(ViewModel.FirstInsertPt - insertPt));
                var turnPt = ViewModel.SecondInsertPt;
                var upText = bushProperties[1] + pipe.DiameterTitle + pipe.Diameter;
                var downText = ViewModel.Elevation;
                var mark = new Mark(ViewModel.FirstInsertPt, turnPt, upText, downText, aimAngle);
                Marks.Add(mark);
                PrintElements();
            }
        }

        void GenerateSubBushes(Dictionary<Point3d, AngelOrUptext> keyValuePairs)
        {
            var PointsToDistance = 1000;            
            using (AcadDatabase adb = AcadDatabase.Active())
            {
                LayerContainer layers = adb.Layers;
                bool flag = false;
                foreach (var layer in layers)
                {
                    if (layer.Name == "W-BUSH")
                        flag = true;                           
                }
                if(!flag)
                    Active.Database.CreateAILayer("W-BUSH", 0);
                var bushProperties = GetBushProperties();//获取套管属性
                foreach (var intersectPoint in keyValuePairs)
                {
                    //插入图块
                    var anglep = intersectPoint.Value.angle;
                    var blkId = adb.CurrentSpace.ObjectId.InsertBlockReference("0", "套管", intersectPoint.Key, new Scale3d(0), Math.Abs(anglep - Math.PI / 2));
                    var br = adb.Element<BlockReference>(blkId);

                    var brAngle = anglep + Math.PI / 2;
                    var brExtSeg = br.GeometricExtents.ToRectangle().GetEdges().First();
                    var v1 = PressureDrainageSystem.Utils.PressureDrainageUtils.CreateVector(intersectPoint.Value.AppendPipeLine);
                    var v2 = PressureDrainageSystem.Utils.PressureDrainageUtils.CreateVector(brExtSeg);
                    if (!(IsParallelVector(v1, v2) || IsPerpVector(v1, v2)))
                    {
                        br.Rotation = brAngle;
                    }

                    blkId.SetDynBlockValue("可见性", bushProperties[0]);
                    br.Layer = "W-BUSH";
                    var insertPt = ((Extents3d)br.Bounds).CenterPoint();
                    br.TransformBy(Matrix3d.Displacement(intersectPoint.Key - insertPt));
                    var moveVec = new Vector3d(1, -1, 0);
                    var matu2w = Active.Editor.UCS2WCS();
                    var matw2u = Active.Editor.WCS2UCS();


                    Point3d turnPt = insertPt.TransformBy(matw2u).TransformBy(Matrix3d.Displacement(moveVec * PointsToDistance)).TransformBy(matu2w);
                    var upText = bushProperties[1] + intersectPoint.Value.UpTxt;
                    var downText = ViewModel.Elevation;
                    var angle = intersectPoint.Value.angle;
                    var mark = new Mark(intersectPoint.Key, turnPt, upText, downText, angle);              
                    Marks.Add(mark);
                }
                PrintElements();
            }
        }

        public static bool IsPerpVector(Vector3d a, Vector3d b, double degreetol = 1)
        {
            double angle = Math.Abs(a.GetAngleTo(b));
            return Math.Abs(Math.Min(angle, Math.Abs(Math.PI * 2 - angle)) / Math.PI * 180 - 90) < degreetol;
        }
        public static bool IsParallelVector(Vector3d a, Vector3d b, double degreetol = 1)
        {
            double angle = Math.Abs(a.GetAngleTo(b));
            return Math.Min(angle, Math.Abs(Math.PI - angle)) / Math.PI * 180 < degreetol;
        }

        void PrintElements()
        {
            if (ViewModel.GenerateTCHElements)
            {
                var tchsymbMultiLeaderService = new TCHDrawSymbMultiLeaderService();
                var symbMultiLeaders = GenerateTCHSymMultiLeader(ViewModel.LayoutFactor);
                tchsymbMultiLeaderService.Init(symbMultiLeaders);
                tchsymbMultiLeaderService.DrawExecute(false);
            }
            else
            {
                GenerateCADLeaders(ViewModel.LayoutFactor);
            }
        }

        void GenerateBushesInBatches()
        {
            using (AcadDatabase adb = AcadDatabase.Active())
            {
                var BOUND = ViewModel.SelectBound;
                //读取数据
                DataReadService dataReadService = new DataReadService(BOUND);
                var readWall = ViewModel.GereratedLeaveHole.Contains("剪力墙");
                var readBeam = ViewModel.GereratedLeaveHole.Contains("梁");
                dataReadService.Read(false, readWall, readBeam);
                var selectedData = dataReadService.SelectDataByBound();
                //筛选数据
                var generatedPipeSystemTypes = ViewModel.GeneratedPipeSystemTypes;
                var generatedLeaveHoleStructTypes = ViewModel.GereratedLeaveHole;
                DataSelectService dataSelectService = new DataSelectService(selectedData.PipeLines, selectedData.Beams, selectedData.ShearWalls);
                dataSelectService.Select(generatedPipeSystemTypes, generatedLeaveHoleStructTypes);
                SelectedData = dataSelectService.SelectModelDatas;
                //求交点
                Dictionary<Point3d, AngelOrUptext> keyValuePairs = new Dictionary<Point3d, AngelOrUptext>();
                keyValuePairs = DataSelectService.GenerateIntersectPointAndAngel(SelectedData.selectPipeLines, SelectedData.Beams, SelectedData.ShearWalls);
                //在交点上生成标注
                GenerateSubBushes(keyValuePairs);
            }
        }
        List<ThTCHSymbMultiLeader> GenerateTCHSymMultiLeader(string layoutFactor)
        {
            var symbMultiLeaders = new List<ThTCHSymbMultiLeader>();
            var uxAxis = Active.Editor.CurrentUserCoordinateSystem.CoordinateSystem3d.Xaxis;
            var angle = uxAxis.GetAngleTo(Vector3d.XAxis);
            angle = angle / Math.PI * 180;
            foreach (var mark in Marks)
            {
                ThTCHSymbMultiLeader symbMultiLeader = new ThTCHSymbMultiLeader(mark.InsertPoint, mark.TurnPoint, GetMarkLength(mark.UpText.Length>=mark.DownText.Length? mark.UpText: mark.DownText, layoutFactor) /100+2, mark.UpText, mark.DownText, "W-BUSH-NOTE",null, layoutFactor);
                symbMultiLeader.LayoutRotation = angle;
                symbMultiLeaders.Add(symbMultiLeader);
            }
            return symbMultiLeaders;
        }
        public double GetMarkLength(string markName,string layoutFactor="1:100")
        {
            double singleLength = 200;
            if (layoutFactor == "1:50")
                singleLength = 100;
            else if(layoutFactor == "1:150")
                singleLength = 250;
            double length = singleLength * markName.Length;
            return length;
        }
        void GenerateCADLeaders(string layoutFactor)
        {
            foreach (var mark in Marks)
            {
                DrawMLeader($"{ mark.UpText}\n{mark.DownText}", mark.InsertPoint, mark.TurnPoint,layoutFactor);
              
            }
        }
        void DrawTraditionalLeaders(Mark mark)
        {
            //绘制图块标注
            Line line = new Line(mark.InsertPoint, mark.TurnPoint);
            line.Layer = mark.MarkLayer;
            line.AddToCurrentSpace();
            var thirdPt = mark.TurnPoint.TransformBy(Matrix3d.Displacement(Vector3d.XAxis * (200 + GetMarkLength(mark.UpText.Length >= mark.DownText.Length ? mark.UpText : mark.DownText))));
            Line newLine = new Line(mark.TurnPoint, thirdPt);
            newLine.Layer = mark.MarkLayer;
            newLine.AddToCurrentSpace();
            //绘制文字标注
            var dbPt = new Point3d(newLine.StartPoint.X + newLine.Length - GetMarkLength(mark.UpText), newLine.StartPoint.Y + 20, 0);
            DBText dB = DrawText(dbPt, mark.UpText, mark.MarkLayer);
            dB.AddToCurrentSpace();
            var dBUnderPt = new Point3d(newLine.StartPoint.X + newLine.Length - GetMarkLength(mark.DownText), newLine.StartPoint.Y - 400, 0);
            DBText dBUnder = DrawText(dBUnderPt, mark.DownText, mark.MarkLayer);
            dBUnder.AddToCurrentSpace();
        }
    }
}
