using System;
using System.Collections.Generic;
using UnityEngine;
using ZombieSurvival.Economy;
using UnityEditor;

public static class WalletT3Checks
{
    [MenuItem("Tools/Character Lab/Check T3 Wallet Contracts")]
    public static string Run()
    {
        var root=new GameObject("T3 checks (temporary)");var passed=new List<string>();
        Action<bool,string> check=(ok,name)=>{if(!ok)throw new Exception("T3 FAILED: "+name);passed.Add(name);};
        Func<string,Wallet> create=name=>{var go=new GameObject(name);go.transform.SetParent(root.transform);return go.AddComponent<Wallet>();};
        try
        {
            var a=create("a");var b=create("b");check(a.Balance==0&&b.Balance==0,"default zero; no inventory currency");
            int events=0;a.Changed+=()=>events++;
            check(!a.TryCredit(0)&&!a.TryDebit(0)&&!a.TryCredit(-1)&&!a.TryDebit(-1)&&!a.TryDebit(long.MinValue)&&events==0,"zero/negative/min amounts rejected without events");
            check(a.TryCredit(250)&&a.TryDebit(100)&&a.Balance==150&&events==2,"credit/debit and Changed once per mutation");
            check(!a.TryDebit(151)&&a.Balance==150&&events==2,"insufficient debit leaves state and events unchanged");
            bool atomic=false; a.Changed+=()=>{if(a.Balance==100&&b.Balance==50)atomic=true;};
            check(a.TryTransferTo(b,50)&&a.Balance==100&&b.Balance==50&&atomic,"atomic transfer commits both before events");
            check(!a.TryTransferTo(a,1)&&!a.TryTransferTo(null,1)&&!a.TryTransferTo(b,-1)&&!a.TryTransferTo(b,0),"invalid/self recipients and zero/negative transfer rejected");
            var max=create("max");check(max.TryCredit(long.MaxValue)&&!max.TryCredit(1)&&max.Balance==long.MaxValue,"long boundary credit prevents overflow");
            long old=a.Balance;check(!a.TryTransferTo(max,1)&&a.Balance==old&&max.Balance==long.MaxValue,"recipient overflow rejects entire transfer");
            a.enabled=false;check(!a.TryCredit(1)&&!b.TryTransferTo(a,1),"disabled wallet refuses mutations/transfers");a.enabled=true;check(a.Balance==old,"enable does not reapply initial balance");
            var budget1=create("budget1");var budget2=create("budget2");budget1.TryCredit(500);budget2.TryCredit(700);
            var adapter=budget1.gameObject.AddComponent<TraderBudget>();check(adapter.TryPay(b,100)&&budget1.Balance==400&&budget2.Balance==700,"finite budgets independent per instance");
            adapter.SetInfinite(true);check(adapter.TryPay(b,long.MaxValue-b.Balance)&&b.Balance==long.MaxValue&&budget1.Balance==400,"infinite source pays without reserve loss or fake max balance");
            check(!adapter.TryPay(b,1)&&budget1.Balance==400,"infinite payment still checks recipient overflow");
            check(adapter.TryReceiveFrom(b,100)&&b.Balance==long.MaxValue-100&&budget1.Balance==400,"infinite sink never grows hidden reserve");
            adapter.enabled=false;adapter.enabled=true;adapter.SetInfinite(false);check(budget1.Balance==400&&!budget1.IsInfinite,"finite/infinite/enable/finite restores same reserve");
            int before=events;a.TryDebit(1);a.TryDebit(1);check(a.Balance==old-2&&events==before+2,"repeated API invocation is a separate explicit request, no ledger");
            return string.Join("\n",passed);
        }
        finally{UnityEngine.Object.DestroyImmediate(root);}
    }
}
