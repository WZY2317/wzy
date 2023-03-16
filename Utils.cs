using AcHelper;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Dreambuild.AutoCAD;
using GeometryExtensions;
using NFox.Cad;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThCADCore.NTS;
using ThMEPEngineCore.CAD;

namespace ThMEPWSS.BushMarked
{
    public class MyLineJig : EntityJig//使用此基类来实现动态绘制只可以绘制一个实体
    {
        //基类EntityJig中有一个关键的成员变量Entity,也就是要绘制到模型空间的临时图形，在绘制完成后将其添加到模型空间以及数据库，既完成了动态绘制
        public MyLineJig(Point3d _basePt) : base(new Line(_basePt, _basePt))//在构造之前调用基类构造为基类成员Entity初始化
        {
            m_AcquirePoint = _basePt;
        }
      

        public static bool StartDrag(ref Point3d p1,ref Point3d p2,ref string msg,ref int pipeIndex)//调用此方法开始绘制
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;

            Point3d startPt = Point3d.Origin;
            PromptStatus promptStatus =  GetPoint("", ref startPt);//需自己实现（获取用户输入点）
            if (promptStatus != PromptStatus.OK)
                return false;

            var mat = Active.Editor.UCS2WCS();
            var originalStartPt = startPt;
            startPt = startPt.TransformBy(mat);

            DataReadService dataReadService = new DataReadService(null);
            dataReadService.Read(true);
            var selectedData = dataReadService.SelectDataByBound();
            var pipe_polys = ModelData.PipeLines.Select(e => e.Polyline).ToList();
            var pipeLineIndex = new ThCADCoreNTSSpatialIndex(pipe_polys.ToCollection());
            var rec = startPt.CreateSquare(200);
            if (!(pipeLineIndex.SelectCrossingPolygon(rec).Count > 0 || pipeLineIndex.SelectFence(rec).Count > 0))
            {
                msg = "请在管线上插入套管。";
                return false;
            }
            else
            {
                if (pipeLineIndex.SelectCrossingPolygon(rec).Count > 0)
                    pipeIndex = pipe_polys.IndexOf(pipeLineIndex.SelectCrossingPolygon(rec).Cast<Polyline>().First());
                else
                    pipeIndex = pipe_polys.IndexOf(pipeLineIndex.SelectFence(rec).Cast<Polyline>().First());
            }
            MyLineJig lineJig = new MyLineJig(startPt);
            PromptResult PR = doc.Editor.Drag(lineJig);//开始绘制
            if (PR.Status != PromptStatus.OK)
                return false;
            var ent = lineJig.Entity;
            p1 = ((Line)ent).StartPoint;
            p2 = ((Line)ent).EndPoint;
            //AppendEntityToModalSpace(doc, lineJig.Entity);//需自己实现（将实体添加进模型空间）
            return true;
        }

        private static PromptStatus GetPoint(string v, ref Point3d startPt)
        {
            var status = Active.Editor.GetPoint(v);
            startPt = status.Value;
            return status.Status;
        }

        private static void AppendEntityToModalSpace(Document doc, Entity entity)
        {
            throw new NotImplementedException();
        }

        protected override SamplerStatus Sampler(JigPrompts prompts)//提取输入数据并处理(鼠标移动时一定频率调用)
        {
            JigPromptPointOptions JPPO = new JigPromptPointOptions();//定义点绘制的配置类
            JPPO.Message = "\n选择点";

            PromptPointResult PR = prompts.AcquirePoint(JPPO);//当鼠标未移动时，程序会在这里阻塞，直到鼠标移动，提取出当前鼠标位置，继续往下运行
            if (PR.Status != PromptStatus.OK)
            {
                return SamplerStatus.Cancel;
            }


            if (PR.Value.DistanceTo(((Line)Entity).EndPoint) < 0.000001f)//若当前鼠标位置离上一次绘制的位置很近，返回NoChange，不让系统去调用Update去刷新
                //此举是为了减少刷新频率，避免绘制时的闪烁
                //（需要注意的是Jig绘制刚开始和结束的瞬间， 即便Sampler返回的是NoChange，也会调用Update）
                return SamplerStatus.NoChange;

            m_AcquirePoint = PR.Value;//更新数据，返回OK,告诉系统，数据已整理好，需要刷新
            return SamplerStatus.OK;
        }

        protected override bool Update()//刷新（方法中应写对Base.Entity的更改,Sampler返回OK时调用）,(在开始绘制时即便Sampler返回NoChange也会调用,绘制的是圆时应避免给圆的半径赋值零,圆实体会退化成点)
        {
            ((Line)Entity).EndPoint = m_AcquirePoint;//利用绘制好的点去改变实体属性(EntityJig内部实现会把Entity成员绘制到模型空间)
            return true;
        }

        private Point3d m_AcquirePoint;//保存提取出来的鼠标位置
    }

    public static class Utils
    {
        public static Polyline PolyFromPoints(Point3d[] points, bool closed = true)
        {
            Polyline p = new Polyline();
            for (int i = 0; i < points.Length; i++)
            {
                p.AddVertexAt(i, points[i].ToPoint2d(), 0, 0, 0);
            }
            p.Closed = closed;
            return p;
        }
        public static Polyline PolyFromLine(Line line)
        {
            Polyline p = new Polyline();
            p.AddVertexAt(0, line.StartPoint.ToPoint2d(), 0, 0, 0);
            p.AddVertexAt(1, line.EndPoint.ToPoint2d(), 0, 0, 0);
            return p;
        }
        public static bool isTCHElement(Entity ent)
        {
            var type = ent.GetType();
            return type.IsNotPublic && type.Name.StartsWith("Imp") && type.Namespace == "Autodesk.AutoCAD.DatabaseServices";
        }
        public static DBText DrawText(Point3d position, string textString,string layer, double height=350,double widthFactor=0.7, string textStyleName= "TH-STYLE3")
        {
            DBText dB = new DBText();
            dB.Position = position;
            dB.TextString = textString;
            dB.Height = height;
            dB.WidthFactor = widthFactor;
            dB.Layer=layer;
            try
            {
                dB.TextStyleId = DbHelper.GetTextStyleId(textStyleName);
            }
            catch { }
            return dB;
        }

    }
}
