using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System.Collections.Generic;
using ThCADExtension;
using Linq2Acad;
using ThMEPEngineCore.Service;

namespace ThMEPWSS.BushMarked
{
    public enum PipeSystemType
    {
        Default,
        Rain,//雨水系统
        SewageAndWaste, //污废水系统
        WaterSupply,//给水系统
        FireHydrant,//消火栓系统
        Spray//喷淋系统
    }
    public class Mark
    {
        public Mark(Point3d insertPoint, Point3d turnPoint, string upText, string downText, double angle = 0,string LayerName="W-BUSH-NOTE" )
        {
            InsertPoint = insertPoint;
            TurnPoint = turnPoint;
            UpText = upText;
            DownText = downText;
            Angle = angle;
            MarkLayer= LayerName;
        }
        public Point3d InsertPoint { get; set; }
        public Point3d TurnPoint { get; set; }
        public string UpText { get; set;}
        public string DownText { get; set; }
        public double Angle { get; set; }
        public string MarkLayer { get; set; }
        

            // public string MarkLayername { get; set;}





        }

        public enum HolesType
    {
        df,
        wall,
        beams
    }

    public class LeaveHolesType
    {
        public LeaveHolesType(Polyline pl)
        {
            Polyline = pl;
        }
        public Polyline Polyline { get; set; }
        public HolesType HolesType { get; set; }
    }
    public class SelectModelData
    {
        public List<PipeLine> selectPipeLines = new List<PipeLine>();
        public List<ShearWall> ShearWalls = new List<ShearWall>();
        public List<Beam> Beams = new List<Beam>();
    }

    public class SelectedModelData
    {
        public SelectedModelData(List<PipeLine> pipeLines, List<ShearWall> shearWalls, List<Beam> beams)
        {
            PipeLines = pipeLines;
            ShearWalls = shearWalls;
            Beams = beams;
        }
        public List<PipeLine> PipeLines = new List<PipeLine>();
        public List<ShearWall> ShearWalls = new List<ShearWall>();
        public List<Beam> Beams = new List<Beam>();
    }
    public static class ModelData
    {
        public static List<PipeLine> PipeLines = null;
        public static List<ShearWall> ShearWalls = null;
        public static List<Beam> Beams = null;
        public static int PipeIndex { get; set; }
    }
    public class PipeLine
    {
        public PipeLine(Polyline pl, PipeSystemType pipeSystem)
        {
            Polyline = pl;
            PipeSystem = pipeSystem;
        }
        public PipeSystemType PipeSystem { get; set; }
        public Polyline Polyline { get; set; }
        public string Diameter = "XXX";
        public string DiameterTitle = "DN";
    }
    public class ShearWall
    {
        public ShearWall(Polyline pl)
        {
            Polyline = pl;
        }
        public Polyline Polyline { get; set; }
    }
    public class Beam
    {
        public Beam(Polyline pl)
        {
            Polyline = pl;
        }
        public Polyline Polyline { get; set; }
    }
    public class MarkProperty
    {

        public Point3d insertPt { get; set; }

        public Point3d turnPt { get; set; }
        public string upText { get; set; }
        public string downText { get; set; }

        public MarkProperty(Point3d insertPt, Point3d turnPt, string upText, string downText)
        {
            this.upText = upText;
            this.downText = downText;
            this.insertPt = insertPt;
            this.turnPt = turnPt;
        }


    }
    public class BushProperty
    {
        public Point3d InterSectPt { get; set; }
        public Polyline BushLine { get; set; }
        public double GetAngle(Polyline polyline, Point3d InterSectPt)
        {
            List<Line> lines = new List<Line>();
            for (int i = 1; i < polyline.NumberOfVertices; i++)
            {
                var line = new Line(polyline.GetPoint3dAt(i), polyline.GetPoint3dAt(i - 1));
                lines.Add(line);
            }
            foreach (Line L in lines)
            {
                if (L.DistanceTo(InterSectPt, false) == 0)
                {
                    return L.Angle;
                    break;
                }

            }
            return 0;


        }
    }
}
