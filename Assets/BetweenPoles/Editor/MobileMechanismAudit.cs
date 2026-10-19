using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
namespace BetweenPoles {
public static class MobileMechanismAudit {
    public static void Run(){
        Directory.CreateDirectory("output/mobile-adaptation");var result=new StringBuilder();
        var assembly=typeof(MobileMechanismAudit).Assembly;
        string[] cases={
            "WalkResponseRegression:Run:output/walk-response.txt",
            "HeightRulesRegression:Run:output/height-rules-result.txt",
            "CrossImpactRegression:Run:output/cross-impact-result.txt",
            "PortalRepulsionRegression:Run:output/portal-repulsion.txt",
            "PortalRepulsionRegression:RunPortalRollingCheck:output/portal-ring-roll-result.txt",
            "PortalRepulsionRegression:RunRingBridgeCheck:output/ring-bridge-result.txt",
            "PortalRepulsionRegression:RunSharedStorage:output/portal-shared-storage.txt",
            "PortalFloatRegression:Run:output/portal-float.txt"
        };
        foreach(var spec in cases){
            var parts=spec.Split(':');string exception=null;
            if(File.Exists(parts[2]))File.Delete(parts[2]);
            try{var type=assembly.GetType("BetweenPoles.EditorTools."+parts[0],true);
                type.GetMethod(parts[1],BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Invoke(null,null);
            }catch(Exception e){exception=(e.InnerException??e).Message;}
            string report=File.Exists(parts[2])?File.ReadAllLines(parts[2])[0]:exception==null?"Completed without a report":"FAIL: "+exception;
            result.AppendLine(parts[0]+"."+parts[1]+": "+report);
            File.WriteAllText("output/mobile-adaptation/mechanics.txt",result.ToString());
        }
        Debug.Log("Mechanism audit complete. Read output/mobile-adaptation/mechanics.txt for individual outcomes.");
    }
}
}
