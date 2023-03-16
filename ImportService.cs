using AcHelper;
using Linq2Acad;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThCADExtension;
using ThMEPEngineCore;

namespace ThMEPWSS.BushMarked
{
    public class ImportService
    {
        public void Import()
        {
            using (AcadDatabase adb = AcadDatabase.Active())
            {
                var file = ThCADCommon.WSSDwgPath();
                using (AcadDatabase Db = AcadDatabase.Open(file, DwgOpenMode.ReadOnly, false))
                {
                    //导入图块
                    List<string> blockNames = new() { "套管" };
                    foreach (var brname in blockNames)
                    {
                        if (!adb.Blocks.Contains(brname))
                            adb.Blocks.Import(Db.Blocks.ElementOrDefault(brname));
                    }               
                }
                //图层
                var layerNames = new List<string>() { "W-BUSH" };
                foreach (var name in layerNames)
                {
                    if(!adb.Layers.Contains(name))
                        Active.Database.CreateAILayer("W-BUSH", 0);
                }
            }
        }
    }
}
