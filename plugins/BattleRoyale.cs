using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("BattleRoyale", "Codex", "0.2.0")]
    [Description("Simple battle royale mode for Rust")]
    public class BattleRoyale : RustPlugin
    {
        #region Configuration
        class ConfigData
        {
            public float StartRadius = 200f;
            public float MinimumRadius = 10f;
            public float ShrinkInterval = 60f;
            public float ShrinkAmount = 20f;
            public float DamageOutside = 5f;
            public bool AutoStart = true;
            public float AutoStartDelay = 5f;
        }

        private ConfigData config;

        protected override void LoadDefaultConfig()
        {
            config = new ConfigData();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<ConfigData>() ?? new ConfigData();
            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(config);
        #endregion

        private Vector3 zoneCenter = Vector3.zero;
        private float zoneRadius = 200f;
        private Timer shrinkTimer;
        private bool eventStarted = false;
        private readonly HashSet<ulong> participants = new HashSet<ulong>();

        void OnServerInitialized()
        {
            if (config.AutoStart)
                timer.Once(config.AutoStartDelay, () => StartEvent());
        }

        [ChatCommand("br.start")]
        private void CmdStart(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) { player.ChatMessage("You are not allowed."); return; }
            StartEvent();
        }

        [ChatCommand("br.stop")]
        private void CmdStop(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) { player.ChatMessage("You are not allowed."); return; }
            EndEvent();
        }

        private void StartEvent()
        {
            if (eventStarted) return;
            eventStarted = true;
            zoneCenter = Vector3.zero; // could be randomized
            zoneRadius = config.StartRadius;
            participants.Clear();
            foreach (var p in BasePlayer.activePlayerList)
            {
                participants.Add(p.userID);
                TeleportToEvent(p);
            }
            PrintToChat("Battle Royale has started!");
            shrinkTimer = timer.Every(config.ShrinkInterval, ShrinkZone);
        }

        private void TeleportToEvent(BasePlayer player)
        {
            Vector3 pos = zoneCenter + (Vector3)(UnityEngine.Random.insideUnitCircle * (config.StartRadius - 5f));
            pos.y = TerrainMeta.HeightMap.GetHeight(pos);
            player.Teleport(pos);
        }

        private void ShrinkZone()
        {
            zoneRadius -= config.ShrinkAmount;
            if (zoneRadius <= config.MinimumRadius)
            {
                zoneRadius = config.MinimumRadius;
                shrinkTimer?.Destroy();
                PrintToChat("Final zone reached!");
            }
            else
            {
                PrintToChat($"Zone has shrunk to {zoneRadius} meters!");
            }
        }

        void OnTick()
        {
            if (!eventStarted) return;
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (!participants.Contains(player.userID)) continue;
                float dist = Vector3.Distance(player.transform.position, zoneCenter);
                if (dist > zoneRadius)
                {
                    player.Hurt(config.DamageOutside * Time.deltaTime, DamageType.Generic);
                }
            }
        }

        void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (!eventStarted || !participants.Contains(player.userID)) return;
            participants.Remove(player.userID);
            CheckForWinner();
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (!eventStarted || !participants.Contains(player.userID)) return;
            participants.Remove(player.userID);
            CheckForWinner();
        }

        private void CheckForWinner()
        {
            if (participants.Count <= 1)
            {
                foreach (var id in participants)
                {
                    var winner = BasePlayer.FindByID(id);
                    winner?.ChatMessage("You are the winner!");
                }
                PrintToChat("Battle Royale has ended!");
                EndEvent();
            }
        }

        private void EndEvent()
        {
            shrinkTimer?.Destroy();
            eventStarted = false;
            participants.Clear();
        }
    }
}
