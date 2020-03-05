﻿using Libs.Actions;
using Libs.GOAP;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Libs.NpcFinder;

namespace Libs
{
    public class Bot
    {
        private GoapAction? currentAction;
        private HashSet<GoapAction> availableActions = new HashSet<GoapAction>();
        private PlayerReader playerReader;
        private PlayerDirection playerDirection;
        private StopMoving stopMoving;
        public GoapAgent Agent;
        public FollowRouteAction followRouteAction;
        public NpcNameFinder npcNameFinder;

        public RouteInfo RouteInfo;

        public bool Active { get; set; }

        public Bot(PlayerReader playerReader)
        {
            this.playerReader = playerReader;
            this.Agent = new GoapAgent(playerReader, this.availableActions);

            var pathText = File.ReadAllText(@"D:\GitHub\WowPixelBot\Badlands41.json");
            var spiritText = File.ReadAllText(@"D:\GitHub\WowPixelBot\Badlands39_SpiritHealer.json");

            var pathPoints = JsonConvert.DeserializeObject<List<WowPoint>>(pathText);
            pathPoints.Reverse();
            var spiritPath = JsonConvert.DeserializeObject<List<WowPoint>>(spiritText);

            this.playerDirection = new PlayerDirection(playerReader, WowProcess);
            this.stopMoving = new StopMoving(WowProcess, playerReader);
            this.npcNameFinder = new NpcNameFinder(WowProcess);
            this.followRouteAction = new FollowRouteAction(playerReader, WowProcess, playerDirection, pathPoints, stopMoving, npcNameFinder);

            RouteInfo = new RouteInfo(pathPoints, spiritPath, this.followRouteAction);
        }

        public async Task DoWork()
        {
            this.currentAction = followRouteAction;

            var killTargetAction = new KillTargetAction(WowProcess, playerReader, stopMoving);

            this.availableActions.Clear();
            this.availableActions.Add(followRouteAction);
            this.availableActions.Add(killTargetAction);
            this.availableActions.Add(new PullTargetAction(WowProcess, playerReader, npcNameFinder, stopMoving));
            this.availableActions.Add(new ApproachTargetAction(WowProcess, playerReader, stopMoving, npcNameFinder));
            this.availableActions.Add(new LootAction(WowProcess, playerReader, stopMoving));
            this.availableActions.Add(new PostKillLootAction(WowProcess, playerReader, stopMoving));
            this.availableActions.Add(new HealAction(WowProcess, playerReader, stopMoving));
            this.availableActions.Add(new TargetDeadAction(WowProcess, playerReader));
            this.availableActions.Add(new WalkToCorpseAction(playerReader, WowProcess, playerDirection, RouteInfo.SpiritPath, RouteInfo.PathPoints, stopMoving));
            this.availableActions.Add(new UseHealingPotionAction(WowProcess, playerReader));
            this.availableActions.Add(new BuffAction(WowProcess, playerReader, stopMoving));

            this.availableActions.ToList().ForEach(a => 
            {
                a.ActionEvent += this.Agent.OnActionEvent;
                a.ActionEvent += killTargetAction.OnActionEvent;
                a.ActionEvent += npcNameFinder.OnActionEvent;
            });



            while (Active)
            {
                await GoapPerformAction();
            }

            await stopMoving.Stop();
            Debug.WriteLine("Stopped!");

        }

        private async Task GoapPerformAction()
        {
            if (this.Agent != null)
            {
                var newAction = await this.Agent.GetAction();

                if (newAction != null)
                {
                    if (newAction != this.currentAction)
                    {
                        this.currentAction?.DoReset();
                        this.currentAction = newAction;
                        Debug.WriteLine("---------------------------------");
                        Debug.WriteLine($"New Plan= {newAction.GetType().Name}");
                    }

                    await newAction.PerformAction();
                }
                else
                {
                    Debug.WriteLine($"New Plan= NULL");
                    Thread.Sleep(500);
                }
            }

        }

        private WowProcess? wowProcess;

        public WowProcess WowProcess
        {
            get
            {
                if (this.wowProcess == null)
                {
                    this.wowProcess = new WowProcess();
                }
                return this.wowProcess;
            }
        }
    }
}
