﻿namespace KoreanOlaf
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using LeagueSharp;
    using LeagueSharp.Common;

    using KoreanOlaf.QueueActions;

    using SharpDX;

    class OlafCore
    {
        private Orbwalking.Orbwalker olafOrbwalker { get; set; }

        private readonly OlafMenu olafMenu;

        private readonly OlafSpell q;

        private readonly OlafSpell w;

        private readonly OlafSpell e;

        private readonly OlafSpell r;

        private readonly Obj_AI_Hero player;

        private readonly OlafOffensiveItems olafItems;

        private readonly ActionQueueList harasQueue;

        private readonly ActionQueueList comboQueue;

        private readonly ActionQueueList laneClearQueue;

        private readonly ActionQueue actionQueue;

        public OlafCore(OlafSpells olafSpells, Orbwalking.Orbwalker olafOrbwalker, OlafMenu olafMenu)
        {
            q = olafSpells.Q;
            w = olafSpells.W;
            e = olafSpells.E;
            r = olafSpells.R;

            player = ObjectManager.Player;
            this.olafOrbwalker = olafOrbwalker;
            this.olafMenu = olafMenu;

            actionQueue = new ActionQueue();
            harasQueue = new ActionQueueList();
            comboQueue = new ActionQueueList();
            laneClearQueue = new ActionQueueList();
            olafItems = new OlafOffensiveItems(olafMenu);

            Game.OnUpdate += Game_OnUpdate;
        }

        private void Game_OnUpdate(EventArgs args)
        {
            switch (olafOrbwalker.ActiveMode)
            {
                case Orbwalking.OrbwalkingMode.Combo:
                    Combo();
                    break;
                case Orbwalking.OrbwalkingMode.Mixed:
                    Harass();
                    break;
                case Orbwalking.OrbwalkingMode.LaneClear:
                    LaneClear();
                    break;
                case Orbwalking.OrbwalkingMode.LastHit:
                    LastHit();
                    break;
                default:
                    return;
            }
        }

        private void Combo()
        {
            if (actionQueue.ExecuteNextAction(comboQueue))
            {
                return;
            }

            if (q.UseOnCombo && q.IsReady())
            {
                Obj_AI_Hero target = TargetSelector.GetTarget(q.Range, q.DamageType);

                if (target != null)
                {
                    PredictionOutput predictionOutput = q.GetPrediction(target);

                    if (predictionOutput.Hitchance >= HitChance.VeryHigh)
                    {
                        q.Cast(predictionOutput.CastPosition.Extend(target.ServerPosition, 100F));
                        return;
                    }
                }
            }

            if (w.UseOnCombo && w.IsReady()
                && TargetSelector.GetTarget(player.AttackRange + 30F, TargetSelector.DamageType.Physical) != null)
            {
                Obj_AI_Hero target = TargetSelector.GetTarget(player.AttackRange + 30F, TargetSelector.DamageType.Physical);

                if (target != null)
                {
                    actionQueue.EnqueueAction(comboQueue, () => true, () => w.Cast(), () => !w.IsReady());
                    actionQueue.EnqueueAction(comboQueue, () => true, () => olafItems.UseItems(target), () => true);
                    actionQueue.EnqueueAction(comboQueue, () => true, () => player.IssueOrder(GameObjectOrder.AttackUnit, target), () => false);
                    return;
                }
            }

            if (e.UseOnCombo && e.IsReady())
            {
                Obj_AI_Hero target = TargetSelector.GetTarget(e.Range, e.DamageType);

                if (target != null)
                {
                    actionQueue.EnqueueAction(comboQueue, () => true, () => player.IssueOrder(GameObjectOrder.AttackUnit, target), () => false);
                    actionQueue.EnqueueAction(comboQueue, () => true, () => olafItems.UseItems(target), () => true);
                    actionQueue.EnqueueAction(comboQueue, () => true, () => e.Cast(target), () => !e.IsReady());
                    actionQueue.EnqueueAction(comboQueue, () => true, () => player.IssueOrder(GameObjectOrder.AttackUnit, target), () => false);
                }
            }
        }

        private void Harass()
        {
            if (actionQueue.ExecuteNextAction(harasQueue))
            {
                return;
            }

            LastHit();

            List<Obj_AI_Hero> blackList = olafMenu.GetHarasBlockList();

            if (q.UseOnHarass && q.IsReady() && CheckManaToHaras())
            {
                Obj_AI_Hero target = TargetSelector.GetTarget(q.Range, q.DamageType, true, blackList);

                if (target != null)
                {
                    PredictionOutput predictionOutput = q.GetPrediction(target);

                    if (predictionOutput.Hitchance >= HitChance.VeryHigh)
                    {
                        q.Cast(predictionOutput.CastPosition.Extend(target.ServerPosition, 50F));
                        return;
                    }
                }
            }

            if (w.UseOnHarass && w.IsReady() && CheckManaToHaras() 
                && TargetSelector.GetTarget(player.AttackRange + 30F, TargetSelector.DamageType.Physical, true, blackList) != null)
            {
                Obj_AI_Hero target = TargetSelector.GetTarget(player.AttackRange + 30F, TargetSelector.DamageType.Physical, true, blackList);

                if (target != null)
                {
                    actionQueue.EnqueueAction(harasQueue, () => true, () => w.Cast(), () => !w.IsReady());
                    actionQueue.EnqueueAction(harasQueue, () => true, () => olafItems.UseHarasItems(), () => true);
                    actionQueue.EnqueueAction(harasQueue, () => true, () => player.IssueOrder(GameObjectOrder.AttackUnit, target), () => false);
                }
            }

            if (e.UseOnHarass && e.IsReady())
            {
                Obj_AI_Hero target = TargetSelector.GetTarget(e.Range, e.DamageType, true, blackList);

                if (target != null)
                {
                    actionQueue.EnqueueAction(harasQueue, () => true, () => player.IssueOrder(GameObjectOrder.AttackUnit, target), () => false);
                    actionQueue.EnqueueAction(harasQueue, () => true, () => olafItems.UseHarasItems(), () => true);
                    actionQueue.EnqueueAction(harasQueue, () => true, () => e.Cast(target), () => !e.IsReady());
                    actionQueue.EnqueueAction(harasQueue, () => true, () => player.IssueOrder(GameObjectOrder.AttackUnit, target), () => false);
                }
            }
        }

        private bool CheckManaToHaras()
        {
            return player.ManaPercent > olafMenu.GetParamSlider("koreanolaf.harasmenu.manalimit");
        }

        private void LaneClear()
        {
            LastHit();

            if (actionQueue.ExecuteNextAction(laneClearQueue))
            {
                return;
            }

            if (q.UseOnLaneClear && q.IsReady() && CheckManaToLaneClear())
            {
                MinionManager.FarmLocation farmLocation = q.GetLineFarmLocation(MinionManager.GetMinions(q.Range));

                if (farmLocation.MinionsHit >= olafMenu.GetParamSlider("koreanolaf.laneclearmenu.useqif"))
                {
                    actionQueue.EnqueueAction(
                        laneClearQueue,
                        () => true,
                        () => q.Cast(farmLocation.Position),
                        () => !q.IsReady());
                    return;
                }
                else
                {
                    Obj_AI_Base jungleMob =
                        MinionManager.GetMinions(q.Range / 1.5F, MinionTypes.All, MinionTeam.Neutral, MinionOrderTypes.MaxHealth)
                            .FirstOrDefault();

                    if (jungleMob != null)
                    {
                        q.Cast(q.GetPrediction(jungleMob).CastPosition);
                    }
                }
            }

            if (w.UseOnLaneClear && w.IsReady() && CheckManaToLaneClear())
            {
                if (MinionManager.GetMinions(300F).Count() >= 3)
                {
                    actionQueue.EnqueueAction(laneClearQueue, () => true, () => w.Cast(), () => !w.IsReady());
                    return;
                }
                else if (
                    MinionManager.GetMinions(
                        300F,
                        MinionTypes.All,
                        MinionTeam.Neutral,
                        MinionOrderTypes.MaxHealth).Any())
                {
                    w.Cast();
                }
            }

            if (e.UseOnLaneClear && e.IsReady() && CheckHealthToLaneClear())
            {
                Obj_AI_Base target =
                    MinionManager.GetMinions(e.Range, MinionTypes.All, MinionTeam.Enemy, MinionOrderTypes.MaxHealth)
                        .FirstOrDefault();

                if (target != null)
                {
                    actionQueue.EnqueueAction(laneClearQueue, () => true, () => e.Cast(target), () => !e.IsReady());
                    return;
                }
            }

            if ((olafMenu.GetParamBool("koreanolaf.laneclearmenu.items.hydra")
                || olafMenu.GetParamBool("koreanolaf.laneclearmenu.items.tiamat")) &&
                (MinionManager.GetMinions(300F).Count() >= olafMenu.GetParamSlider("koreanolaf.laneclearmenu.items.when")
                || MinionManager.GetMinions(300F, MinionTypes.All, MinionTeam.Neutral, MinionOrderTypes.MaxHealth).Any()))
            {
                actionQueue.EnqueueAction(laneClearQueue, () => true, () => olafItems.UseItemsLaneClear(), () => true);
                
            }
        }

        private bool CheckManaToLaneClear()
        {
            return player.ManaPercent > olafMenu.GetParamSlider("koreanolaf.laneclearmenu.manalimit");
        }

        private bool CheckHealthToLaneClear()
        {
            return player.HealthPercent > olafMenu.GetParamSlider("koreanolaf.laneclearmenu.healthlimit");
        }

        private void LastHit()
        {
            if (q.UseOnLastHit && q.IsReady() && CheckManaToLastHit())
            {
                Obj_AI_Base target =
                    MinionManager.GetMinions(q.Range, MinionTypes.All, MinionTeam.Enemy, MinionOrderTypes.MaxHealth)
                        .FirstOrDefault(
                            minion =>
                            minion.Distance(player) > Orbwalking.GetRealAutoAttackRange(minion) + 10F
                            && !minion.IsDead
                            && q.GetDamage(minion) > minion.Health);

                if (target != null)
                {
                    PredictionOutput predictionOutput = q.GetPrediction(target);
                    q.Cast(predictionOutput.CastPosition);
                }
            }

            if (e.UseOnLastHit && e.IsReady() && CheckManaToLastHit())
            {
                Obj_AI_Base target =
                    MinionManager.GetMinions(e.Range, MinionTypes.All, MinionTeam.Enemy, MinionOrderTypes.MaxHealth)
                        .FirstOrDefault(minion => e.GetDamage(minion) > minion.Health);

                if (target != null 
                    && olafMenu.GetParamBool("koreanolaf.miscmenu.savee")
                    && MinionManager.GetMinions(e.Range * 2)
                           .Any(minion => minion.CharData.Name.ToLowerInvariant().Contains("cannon") 
                                && !minion.IsDead)
                    && !target.CharData.Name.ToLowerInvariant().Contains("cannon"))
                {
                    return;
                }

                if (target != null)
                {
                    e.Cast(target);
                }
                else if (
                    MinionManager.GetMinions(
                        e.Range,
                        MinionTypes.All,
                        MinionTeam.Neutral,
                        MinionOrderTypes.MaxHealth).Any())
                {
                    Obj_AI_Base jungleTarget =
                        MinionManager.GetMinions(
                            e.Range,
                            MinionTypes.All,
                            MinionTeam.Neutral,
                            MinionOrderTypes.MaxHealth).FirstOrDefault(minion => e.GetDamage(minion) > minion.Health);
                    if (jungleTarget != null)
                    {
                        e.Cast(jungleTarget);
                    }
                }
            }
        }

        private bool CheckManaToLastHit()
        {
            return player.ManaPercent > olafMenu.GetParamSlider("koreanolaf.lasthitmenu.manalimit");
        }

        public float ComboDamage(Obj_AI_Hero target)
        {
            float result = q.UseOnCombo && q.IsReady() ? q.GetDamage(target): 0F;

            result += e.UseOnCombo && e.IsReady() ? e.GetDamage(target) : 0F;

            return result;
        }

        public void ForceUltimate(Obj_AI_Hero target = null)
        {
            if (target != null && r.CanCast(target))
            {
                r.Cast(target);
            }
            else
            {
                r.Cast(TargetSelector.GetTarget(r.Range, r.DamageType));
            }
        }
    }
}