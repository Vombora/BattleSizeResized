using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.TroopSuppliers;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace BattleSizeResized
{
    [HarmonyPatch]
    internal class Patches
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameTextManager), "FindText")] // xslt not supported for UI texts.
        public static void Postfix(ref TextObject __result, string id, string variation = null)
        {
            try
            {
                string root = "str_options_type_BattleSize_";

                if (!id.Contains(root))
                {
                    return;
                }

                TextObject txt = null;
                string[] texts = new string[]
                {
                    "Very Low",
                    "Low",
                    "Medium",
                    "High",
                    "Very High",
                    "Ultra",
                    "Engine Max"
                };

                for (int i = 0; i < texts.Length; i++)
                {

                    if (id.Equals(root + i.ToString()))
                    {
                        txt = new TextObject(texts[i] + " (" + Configs._battleSizes[i].ToString() + ")", null);
                    }
                }
                if (txt != null)
                {
                    __result = txt;
                }
            }
            catch (Exception ex)
            {
                TxtLogger.TryWarnAndLog(ex);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(BannerlordConfig), "MaxBattleSize", MethodType.Getter)]
        public static void MaxBattleSizePostfix(ref int __result)
        {
            __result = 10000;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(BannerlordConfig), "GetRealBattleSize")]
        public static void GetRealBattleSizePostfix(ref int __result)
        {
            __result = Configs._battleSizes[BannerlordConfig.BattleSize] > 1999 ? 2040 : Configs._battleSizes[BannerlordConfig.BattleSize];
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(BannerlordConfig), "GetRealBattleSizeForSiege")]
        public static void GetRealBattleSizeForSiegePostfix(ref int __result)
        {
            __result = Configs._battleSizes[BannerlordConfig.BattleSize] > 1999 ? 2040 : Configs._battleSizes[BannerlordConfig.BattleSize];
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(BannerlordConfig), "GetRealBattleSizeForSallyOut")]
        public static void GetRealBattleSizeForSallyOutPostfix(ref int __result)
        {
            __result = Configs._sallyOutBattleSizes[BannerlordConfig.BattleSize];
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Agent), "Die")]
        public static void Postfix(Agent __instance, Blow b, Agent.KillInfo overrideKillInfo = Agent.KillInfo.Invalid)
        {
            try
            {
                if (__instance != Agent.Main && __instance.MountAgent != null && __instance.Mission.AllAgents.Count > 1000)
                {
                    __instance.MountAgent.Die(b, overrideKillInfo);
                }
            }
            catch (Exception ex)
            {
                TxtLogger.TryWarnAndLog(ex);
            }
        }

        private static readonly Type MissionSideType = AccessTools.TypeByName("TaleWorlds.MountAndBlade.MissionAgentSpawnLogic+MissionSide");

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MissionAgentSpawnLogic), "MaxNumberOfTroopsForMission", MethodType.Getter)]
        static void Postfix(ref int __result)
        {
            if (__result > 999)
            {
                __result = 2040;
            }            
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MissionAgentSpawnLogic), "Init")]
        public static bool InitPrefix(MissionAgentSpawnLogic __instance, bool spawnDefenders, bool spawnAttackers, in MissionSpawnSettings reinforcementSpawnSettings, ref MissionSpawnSettings ____spawnSettings, ref float ____globalReinforcementInterval)
        {
            try
            {
                FieldInfo phasesField = AccessTools.Field(typeof(MissionAgentSpawnLogic), "_phases");
                object phasesObj = phasesField.GetValue(__instance);
                Array phasesArray = phasesObj as Array;
                if (phasesArray == null)
                    return true;

                var missionSidesField = AccessTools.Field(typeof(MissionAgentSpawnLogic), "_missionSides");
                var missionSides = (Array)missionSidesField.GetValue(__instance);

                for (int i = 0; i < phasesArray.Length; i++)
                {
                    var list = phasesArray.GetValue(i) as IList;
                    if (list == null || list.Count <= 0)
                        return true;
                }

                FieldInfo spawnSettingsField = AccessTools.Field(typeof(MissionAgentSpawnLogic), "_spawnSettings");
                spawnSettingsField.SetValue(__instance, reinforcementSpawnSettings);
                FieldInfo globalReinforcementField = AccessTools.Field(typeof(MissionAgentSpawnLogic), "_globalReinforcementInterval");
                globalReinforcementField.SetValue(__instance, reinforcementSpawnSettings.GlobalReinforcementInterval);

                int defenderIndex = 0;
                int attackerIndex = 1;

                Type spawnPhaseType = AccessTools.Inner(typeof(MissionAgentSpawnLogic), "SpawnPhase");
                FieldInfo totalSpawnField = AccessTools.Field(spawnPhaseType, "TotalSpawnNumber");
                FieldInfo initialSpawnField = AccessTools.Field(spawnPhaseType, "InitialSpawnNumber");
                FieldInfo remainingSpawnField = AccessTools.Field(spawnPhaseType, "RemainingSpawnNumber");

                int[] totals = new int[2];
                for (int side = 0; side < 2; side++)
                {
                    int sum = 0;
                    var list = phasesArray.GetValue(side) as IEnumerable;
                    foreach (object phase in list)
                    {
                        sum += (int)totalSpawnField.GetValue(phase);
                    }
                    totals[side] = sum;
                }
                int grandTotal = totals.Sum();

                if (reinforcementSpawnSettings.InitialTroopsSpawnMethod == MissionSpawnSettings.InitialSpawnMethod.BattleSizeAllocating)
                {
                    float[] ratios = new float[2];
                    ratios[defenderIndex] = (float)totals[defenderIndex] / (float)grandTotal;
                    ratios[attackerIndex] = (float)totals[attackerIndex] / (float)grandTotal;
                    ratios[defenderIndex] = TaleWorlds.Library.MathF.Min(reinforcementSpawnSettings.MaximumBattleSideRatio,
                                                      ratios[defenderIndex] * reinforcementSpawnSettings.DefenderAdvantageFactor);
                    ratios[attackerIndex] = 1f - ratios[defenderIndex];

                    int dominant = (ratios[defenderIndex] >= ratios[attackerIndex]) ? defenderIndex : attackerIndex;
                    int opposite = dominant == 0 ? 1 : 0;

                    if (ratios[opposite] > reinforcementSpawnSettings.MaximumBattleSideRatio)
                    {
                        ratios[opposite] = reinforcementSpawnSettings.MaximumBattleSideRatio;
                        ratios[dominant] = 1f - reinforcementSpawnSettings.MaximumBattleSideRatio;
                    }

                    int[] desired = new int[2];
                    FieldInfo battleSizeField = AccessTools.Field(typeof(MissionAgentSpawnLogic), "_battleSize");
                    int battleSize = (int)battleSizeField.GetValue(__instance);

                    int calculated = TaleWorlds.Library.MathF.Ceiling(ratios[dominant] * (float)battleSize);
                    desired[dominant] = Math.Min(calculated, totals[dominant]);
                    desired[opposite] = battleSize - desired[dominant];
                    if (desired[opposite] > totals[opposite])
                    {
                        desired[opposite] = totals[opposite];
                        desired[dominant] = Math.Min(battleSize - desired[opposite], totals[dominant]);
                    }

                    Queue<IAgentOriginBase>[] remainingTroops = GetTroopOrderDueToRetardedCode(__instance, missionSides);

                    int[] initialSpawnNumbers = new int[2] { 0, 0 };                                        

                    if (desired.Sum() > 999 && !Mission.Current.IsSiegeBattle)
                    {
                        int higherAllocationSide = (desired[defenderIndex] >= desired[attackerIndex]) ? defenderIndex : attackerIndex, lowerAllocationSide = higherAllocationSide == 0 ? 1 : 0;

                        int missionAgentRoom = 2040;

                        int[] CustomTeamBattleSizes = new int[2] { desired[0], desired[1] };

                        float originalRatio = (float)desired[lowerAllocationSide] / desired[higherAllocationSide];

                        while (missionAgentRoom > 1 && ((remainingTroops[dominant].Any() && CustomTeamBattleSizes[dominant] > 0) || (remainingTroops[opposite].Any() && CustomTeamBattleSizes[opposite] > 0)))
                        {
                            for (int side = 0; side < 2 && missionAgentRoom > 1; side++)
                            {
                                if (side == lowerAllocationSide && initialSpawnNumbers[higherAllocationSide] > 0)
                                {
                                    float dynamicRatio = (float)initialSpawnNumbers[lowerAllocationSide] / (float)initialSpawnNumbers[higherAllocationSide];
                                    if (dynamicRatio > originalRatio) continue;
                                }
                                var missionSide = missionSides.GetValue(side);

                                if (remainingTroops[side].Any() && CustomTeamBattleSizes[side] > 0)
                                {
                                    int agentCost = 2;
                                    bool spawnWithHorses = (bool)AccessTools.Field(MissionSideType, "_spawnWithHorses").GetValue(missionSide);

                                    //Can't use /*!remainingTroops[side].Peek().Troop.IsMounted*/ because custom troop mods have wrong formations assigned to xml

                                    if (!spawnWithHorses || remainingTroops[side].Peek().Troop.Equipment.Horse.IsEmpty)
                                    {
                                        agentCost = 1;
                                    }

                                    missionAgentRoom -= agentCost;
                                    initialSpawnNumbers[side]++;
                                    remainingTroops[side].Dequeue();
                                    CustomTeamBattleSizes[side]--;
                                }
                            }
                        }
                    }
                    else
                    {
                        initialSpawnNumbers = desired;
                        if (Mission.Current.IsSiegeBattle && battleSize > 1999 && totals.Sum() > 2039)
                        {
                            for (int side = 0; side < 2; side++)
                            {
                                var character = remainingTroops[side].Peek().Troop;
                                if (character.IsPlayerCharacter && !character.Equipment.Horse.IsEmpty)
                                {
                                    initialSpawnNumbers[side]--;
                                }
                            }
                        }                                                
                    }

                    for (int side = 0; side < 2; side++)
                    {
                        var list = phasesArray.GetValue(side) as IEnumerable;
                        foreach (object phase in list)
                        {
                            int initialVal = (int)initialSpawnField.GetValue(phase);
                            if (initialVal > initialSpawnNumbers[side])
                            {
                                int diff = initialVal - initialSpawnNumbers[side];
                                initialSpawnField.SetValue(phase, initialSpawnNumbers[side]);
                                int rem = (int)remainingSpawnField.GetValue(phase);
                                remainingSpawnField.SetValue(phase, rem + diff);
                            }
                        }
                    }
                }
                else if (reinforcementSpawnSettings.InitialTroopsSpawnMethod == MissionSpawnSettings.InitialSpawnMethod.FreeAllocation)
                {
                    
                }

                if (reinforcementSpawnSettings.ReinforcementTroopsSpawnMethod == MissionSpawnSettings.ReinforcementSpawnMethod.Wave)
                {
                    for (int side = 0; side < 2; side++)
                    {
                        var list = phasesArray.GetValue(side) as IEnumerable;
                        foreach (object phase in list)
                        {
                            int initialVal = (int)initialSpawnField.GetValue(phase);
                            int num8 = (int)Math.Max(1f, initialVal * reinforcementSpawnSettings.ReinforcementWavePercentage);
                            if (reinforcementSpawnSettings.MaximumReinforcementWaveCount > 0)
                            {
                                int remVal = (int)remainingSpawnField.GetValue(phase);
                                int maxWave = Math.Min(remVal, num8 * reinforcementSpawnSettings.MaximumReinforcementWaveCount);
                                int diff = Math.Max(0, remVal - maxWave);

                                FieldInfo troopsField = AccessTools.Field(typeof(MissionAgentSpawnLogic), "_numberOfTroopsInTotal");
                                int[] troopTotals = (int[])troopsField.GetValue(__instance);
                                troopTotals[side] -= diff;
                                totals[side] -= diff;

                                remainingSpawnField.SetValue(phase, maxWave);
                                int initSpawn = (int)initialSpawnField.GetValue(phase);
                                totalSpawnField.SetValue(phase, initSpawn + maxWave);
                            }
                        }
                    }
                }

                object mission = AccessTools.Property(__instance.GetType().BaseType, "Mission").GetValue(__instance);
                object defenderPhase = GetFirstElement(phasesArray.GetValue(defenderIndex));
                object attackerPhase = GetFirstElement(phasesArray.GetValue(attackerIndex));
                if (defenderPhase == null || attackerPhase == null)
                    return true;
                int defenderInitial = (int)initialSpawnField.GetValue(defenderPhase);
                int attackerInitial = (int)initialSpawnField.GetValue(attackerPhase);                

                AccessTools.Method(mission.GetType(), "SetBattleAgentCount").Invoke(mission, new object[] { Math.Min(defenderInitial, attackerInitial) });

                AccessTools.Method(mission.GetType(), "SetInitialAgentCountForSide", new[] { typeof(BattleSideEnum), typeof(int) })
                           .Invoke(mission, new object[] { BattleSideEnum.Defender, totals[defenderIndex] });
                AccessTools.Method(mission.GetType(), "SetInitialAgentCountForSide", new[] { typeof(BattleSideEnum), typeof(int) })
                           .Invoke(mission, new object[] { BattleSideEnum.Attacker, totals[attackerIndex] });

                object missionSidesObj = missionSidesField.GetValue(__instance);
                Array missionSidesArray = missionSidesObj as Array;
                if (missionSidesArray != null)
                {
                    object defenderSide = missionSidesArray.GetValue(defenderIndex);
                    object attackerSide = missionSidesArray.GetValue(attackerIndex);
                    MethodInfo setSpawnMethod = AccessTools.Method(defenderSide.GetType(), "SetSpawnTroops");
                    setSpawnMethod.Invoke(defenderSide, new object[] { spawnDefenders });
                    setSpawnMethod.Invoke(attackerSide, new object[] { spawnAttackers });
                }

                return false;
            }
            catch (Exception ex)
            {
                TxtLogger.TryWarnAndLog(ex);
                return true;
            }
        }

        private static object GetFirstElement(object listObj)
        {
            if (listObj is IEnumerable enumerable)
            {
                foreach (object item in enumerable)
                    return item;
            }
            return null;
        }

        private static Queue<IAgentOriginBase>[] GetTroopOrderDueToRetardedCode(MissionAgentSpawnLogic instance, Array missionSides)
        {
            IEnumerable<IAgentOriginBase>[] originsEnumerable;

            if (Game.Current.GameType != null && Game.Current.GameType is Campaign)
            {
                originsEnumerable = new IEnumerable<IAgentOriginBase>[2]
                {
                    GetOrderedTroopList((PartyGroupTroopSupplier)AccessTools.Field(MissionSideType, "_troopSupplier").GetValue(missionSides.GetValue(0))),
                    GetOrderedTroopList((PartyGroupTroopSupplier)AccessTools.Field(MissionSideType, "_troopSupplier").GetValue(missionSides.GetValue(1)))
                };
            }
            else
            {
                originsEnumerable = new IEnumerable<IAgentOriginBase>[2]
                {
                    GetCustomBattleOrderedTroopList((CustomBattleTroopSupplier)AccessTools.Field(MissionSideType, "_troopSupplier").GetValue(missionSides.GetValue(0))),
                    GetCustomBattleOrderedTroopList((CustomBattleTroopSupplier)AccessTools.Field(MissionSideType, "_troopSupplier").GetValue(missionSides.GetValue(1)))
                };
            }            

            return new Queue<IAgentOriginBase>[] { new(originsEnumerable[0].ToList()), new(originsEnumerable[1].ToList()) };
        }

        public static IEnumerable<IAgentOriginBase> GetOrderedTroopList(object troopSupplier)
        {
            var partyGroup = (MapEventSide)AccessTools.Property(typeof(PartyGroupTroopSupplier), "PartyGroup").GetValue(troopSupplier);

            List<UniqueTroopDescriptor> list = null;

            GetOrderedTroopsPriorityList(ref list, partyGroup);

            PartyGroupAgentOrigin[] array = new PartyGroupAgentOrigin[list.Count];

            ConstructorInfo _constructor = AccessTools.Constructor(typeof(PartyGroupAgentOrigin),
                new[]
                {
                typeof(PartyGroupTroopSupplier),
                typeof(UniqueTroopDescriptor),
                typeof(int)
                }
            );

            for (int i = 0; i < array.Length; i++)
            {
                array[i] = (PartyGroupAgentOrigin)_constructor.Invoke(new object[] { troopSupplier, list[i], i });
            }

            return array;
        }

        private static void GetOrderedTroopsPriorityList(ref List<UniqueTroopDescriptor> troopsList, MapEventSide partyGroup)
        {
            var readyTroopsPriorityList = (List<ValueTuple<FlattenedTroopRosterElement, MapEventParty, float>>)AccessTools.Field(typeof(MapEventSide), "_readyTroopsPriorityList").GetValue(partyGroup);

            List<ValueTuple<FlattenedTroopRosterElement, MapEventParty, float>> orderedTroopPriorityList = new(readyTroopsPriorityList.OrderByDescending(x => x.Item3));

            if (troopsList == null)
            {
                troopsList = new List<UniqueTroopDescriptor>();
            }
            else
            {
                troopsList.Clear();
            }

            foreach (var valueTuple in orderedTroopPriorityList)
            {
                troopsList.Add(valueTuple.Item1.Descriptor);
            }
        }

        public static IEnumerable<IAgentOriginBase> GetCustomBattleOrderedTroopList(CustomBattleTroopSupplier troopSupplier)
        {
            List<BasicCharacterObject> list = GetOrderedTroopsPriorityListForCustomBattle((CustomBattleTroopSupplier)troopSupplier);

            CustomBattleAgentOrigin[] array = new CustomBattleAgentOrigin[list.Count];
            var customBattleCombatant = (CustomBattleCombatant)AccessTools.Field(typeof(CustomBattleTroopSupplier), "_customBattleCombatant").GetValue(troopSupplier);

            var isPlayerSide = (bool)AccessTools.Field(typeof(CustomBattleTroopSupplier), "_isPlayerSide").GetValue(troopSupplier);

            for (int i = 0; i < array.Length; i++)
            {
                UniqueTroopDescriptor uniqueNo = new UniqueTroopDescriptor(GetNextUniqueTroopSeed());
                array[i] = new CustomBattleAgentOrigin(customBattleCombatant, list[i], troopSupplier, isPlayerSide, i, uniqueNo);
            }

            return array;
        }

        private static List<BasicCharacterObject> GetOrderedTroopsPriorityListForCustomBattle(CustomBattleTroopSupplier troopSupplier)
        {
            TaleWorlds.Library.PriorityQueue<float, BasicCharacterObject> originalCharactersQueue = (TaleWorlds.Library.PriorityQueue<float, BasicCharacterObject>)AccessTools.Field(typeof(CustomBattleTroopSupplier), "_characters").GetValue(troopSupplier);

            TaleWorlds.Library.PriorityQueue<float, BasicCharacterObject> characters = new TaleWorlds.Library.PriorityQueue<float, BasicCharacterObject>(originalCharactersQueue);

            List<BasicCharacterObject> list = new List<BasicCharacterObject>();

            while (characters.Count > 0)
            {
                BasicCharacterObject basicCharacterObject = characters.DequeueValue();                
                list.Add(basicCharacterObject);
            }

            return list;
        }

        static int _nextUniqueTroopSeed;

        private static int GetNextUniqueTroopSeed()
        {
            if (_nextUniqueTroopSeed == null)
            {
                _nextUniqueTroopSeed = Game.Current.NextUniqueTroopSeed;
            }

            int nextUniqueTroopSeed = _nextUniqueTroopSeed;
            _nextUniqueTroopSeed = nextUniqueTroopSeed + 1;
            return nextUniqueTroopSeed;
        }        
    }
}
