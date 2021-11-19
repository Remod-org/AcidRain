#region License (GPL v3)
/*
    NextGenPVE - Prevent damage to players and objects in a Rust PVE environment
    Copyright (c) 2020 RFC1920 <desolationoutpostpve@gmail.com>

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/
#endregion License (GPL v3)
using Network;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Acid Rain", "RFC1920", "1.0.9")]
    [Description("The rain can kill you - take cover!")]

    internal class AcidRain : RustPlugin
    {
        private ConfigData configData;
        public static AcidRain Instance;
        public List<ulong> protectedPlayers = new List<ulong>();
        private bool PluginEnabled;
        private const string permAdmin = "acidrain.admin";

        #region Message
        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        private void Message(IPlayer player, string key, params object[] args) => player.Message(Lang(key, player.Id, args));
        #endregion

        #region hooks
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["seekshelter"] = "The ACID RAIN will KILL YOU!  Seek shelter NOW!!!",
                ["notauthorized"] = "You are not authorized for this command!",
                ["beware"] = "Beware of the ACID RAIN!",
                ["enabled"] = "Acid Rain has been ENabled.",
                ["disabled"] = "Acid Rain has been DISabled.",
                ["innoculated"] = "All players have been innoculated!",
                ["innoculation"] = "You have been innoculated!",
                ["protectedm"] = "You have about {0} minutes to protect yourself from the ACID RAIN!",
                ["recovered"] = "You have recovered from the ACID RAIN."
            }, this);
        }

        private void Loaded()
        {
            Instance = this;
            permission.RegisterPermission(permAdmin, this);
            AddCovalenceCommand("inno", "CmdInnoculate");
            AddCovalenceCommand("arstop", "CmdDisable");
            AddCovalenceCommand("arstart", "CmdEnable");

            LoadConfigVariables();
            PluginEnabled = configData.Options.EnableOnLoad;

            if (PluginEnabled)
            {
                foreach (BasePlayer pl in BasePlayer.activePlayerList)
                {
                    if (configData.Options.debug) Puts($"Adding object to active player {pl.UserIDString}");
                    AddRadComponent(pl);
                }

                if (configData.Options.damageSleepers)
                {
                    foreach (BasePlayer pl in BasePlayer.sleepingPlayerList)
                    {
                        if (configData.Options.debug) Puts($"Adding object to sleeper {pl.UserIDString}");
                        AddRadComponent(pl);
                    }
                }
            }
        }

        private void OnServerShutdown() => Unload();

        private void Unload()
        {
            foreach (BasePlayer pl in BasePlayer.activePlayerList)
            {
                if (pl.GetComponent<AcidRads>())
                {
                    UnityEngine.Object.Destroy(pl.gameObject.GetComponent<AcidRads>());
                }
            }
            foreach (BasePlayer pl in BasePlayer.sleepingPlayerList)
            {
                if (pl.gameObject.GetComponent<AcidRads>())
                {
                    UnityEngine.Object.Destroy(pl.gameObject.GetComponent<AcidRads>());
                }
            }
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (PluginEnabled)
            {
                AddRadComponent(player);
                AddPlayerProtection(player);
            }
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (!configData.Options.damageSleepers)
            {
                AcidRads c = player.gameObject.GetComponent<AcidRads>();
                if (c != null)
                {
                    UnityEngine.Object.Destroy(c);
                }
            }
        }

        private void OnUserRespawned(IPlayer player)
        {
            if (player == null) return;
            if (PluginEnabled)
            {
                AddRadComponent(player.Object as BasePlayer);
                AddPlayerProtection(player.Object as BasePlayer);
            }
        }
        #endregion hooks

        #region config
        private void LoadConfigVariables()
        {
            configData = Config.ReadObject<ConfigData>();

            configData.Version = Version;
            SaveConfig(configData);
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Creating new config file.");
            ConfigData config = new ConfigData
            {
                Options = new Options()
                {
                    hilevelbump = 1f,
                    hipoisonbump = 0.5f,
                    lolevelbump = 0.2f,
                    lopoisonbump = 0.1f,
                    notifyTimer = 60f,
                    protectionTimer = 300f,
                    damageSleepers = false,
                    EnableOnLoad = true,
                    debug = false
                },
                Version = Version
            };

            SaveConfig(config);
        }

        private void SaveConfig(ConfigData config)
        {
            Config.WriteObject(config, true);
        }
        #endregion

        #region helper
        private void SendEffectTo(string effect, BasePlayer player)
        {
            if (player == null) return;

            Effect EffectInstance = new Effect();
            EffectInstance.Init(Effect.Type.Generic, player, 0, Vector3.up, Vector3.zero);
            EffectInstance.pooledstringid = StringPool.Get(effect);
            Net.sv.write.Start();
            Net.sv.write.PacketID(Network.Message.Type.Effect);
            EffectInstance.WriteToStream(Net.sv.write);
            Net.sv.write.Send(new SendInfo(player.net.connection));
            EffectInstance.Clear();
        }

        private void AddRadComponent(BasePlayer player)
        {
            if (player.gameObject.GetComponent<AcidRads>())
            {
                UnityEngine.Object.Destroy(player.gameObject.GetComponent<AcidRads>());
            }
            AcidRads c = player.gameObject.AddComponent<AcidRads>();
            c.Options = new Options
            {
                lolevelbump = configData.Options.lolevelbump,
                lopoisonbump = configData.Options.lopoisonbump,
                hilevelbump = configData.Options.hilevelbump,
                hipoisonbump = configData.Options.hipoisonbump,
                notifyTimer = configData.Options.notifyTimer
            };
        }

        private void AddPlayerProtection(BasePlayer player)
        {
            Message(player.IPlayer, "protectedm", Math.Floor(configData.Options.protectionTimer / 60).ToString());
            protectedPlayers.Add(player.userID);
            if (configData.Options.debug) Puts($"Starting protection timer for {player.userID}");
            timer.Once(configData.Options.protectionTimer, () => RemovePlayerProtection(player));
        }

        private void RemovePlayerProtection(BasePlayer player)
        {
            if (configData.Options.debug) Puts($"Removing protection timer for {player.UserIDString}");
            protectedPlayers.Remove(player.userID);
            Message(player.IPlayer, "beware");
        }
        #endregion

        #region commands
        [Command("arstop")]
        private void CmdDisable(IPlayer iplayer, string command, string[] args)
        {
            if (!iplayer.HasPermission(permAdmin)) { Message(iplayer, "notauthorized"); return; }
            PluginEnabled = false;
            Message(iplayer, "disabled");
        }

        [Command("arstart")]
        private void CmdEnable(IPlayer iplayer, string command, string[] args)
        {
            if (!iplayer.HasPermission(permAdmin)) { Message(iplayer, "notauthorized"); return; }
            PluginEnabled = true;
            Message(iplayer, "enabled");
        }

        [Command("inno")]
        private void CmdInnoculate(IPlayer iplayer, string command, string[] args)
        {
            if (!iplayer.HasPermission(permAdmin)) { Message(iplayer, "notauthorized"); return; }
            if (args.Length > 0 && args[0] == "stop")
            {
                PluginEnabled = false;
                Message(iplayer, "disabled");
            }

            foreach (BasePlayer pl in BasePlayer.activePlayerList)
            {
                if (configData.Options.debug) Puts($"Innoculating {pl.displayName}");
                if (pl.isActiveAndEnabled)
                {
                    SendEffectTo("assets/prefabs/tools/medical syringe/effects/inject_friend.prefab", pl);
                    if (pl.IsWounded()) pl.StopWounded();
                    pl.Heal(100f);
                    SendReply(pl, Instance.Lang("innoculation"));
                }
            }

            if (configData.Options.damageSleepers)
            {
                foreach (BasePlayer pl in BasePlayer.sleepingPlayerList)
                {
                    if (configData.Options.debug) Puts($"Innoculating {pl.displayName}");
                    if (pl.isActiveAndEnabled)
                    {
                        SendEffectTo("assets/prefabs/tools/medical syringe/effects/inject_friend.prefab", pl);
                        if (pl.IsWounded()) pl.StopWounded();
                        pl.Heal(100f);
                        SendReply(pl, Instance.Lang("innoculation"));
                    }
                }
            }

            Message(iplayer, "innoculated");
        }
        #endregion

        #region classes
        private class ConfigData
        {
            public Options Options;
            public VersionNumber Version;
        }

        public class Options
        {
            public float hilevelbump;
            public float hipoisonbump;
            public float lolevelbump;
            public float lopoisonbump;
            public float notifyTimer;
            public float protectionTimer;
            public bool damageSleepers;
            public bool EnableOnLoad;
            public bool debug;
        }

        private class AcidRads : MonoBehaviour
        {
            public Options Options = new Options();
            private Timer timer;

            private BasePlayer player;
            private bool notified;

            private void Awake() => player = GetComponentInParent<BasePlayer>();

            private void OnDestroy()
            {
                timer?.Destroy();
            }

            private void NotifyTimer(bool start = false)
            {
                if (timer?.Destroyed == true)
                {
                    notified = false;
                }
                if (start)
                {
                    timer = Instance.timer.Once(Options.notifyTimer, () => NotifyTimer());
                }
            }

            private float CheckProtection()
            {
                if (!Instance.PluginEnabled) return 0f;
                float scale = 1f;
                string sleeping = "";
                foreach (Item item in player.inventory.containerWear.itemList)
                {
                    if (Instance.configData.Options.debug) Instance.Puts($"Player {player.displayName} wearing {item.info.name}");
                    if (item.info.name.Contains("hat") || item.info.name.Contains("cap"))
                    {
                        scale *= 0.99f;
                    }
                    if (item.info.name.Contains("shirt") || item.info.name.Contains("short"))
                    {
                        scale *= 0.98f;
                    }
                    if (item.info.name.Contains("Boot"))
                    {
                        scale *= 0.97f;
                    }
                    if (item.info.name.Contains("gloves"))
                    {
                        scale *= 0.96f;
                    }
                    else if (item.info.name.Contains("pants"))
                    {
                        scale *= 0.95f;
                    }
                    if (item.info.name.Equals("poncho.hide.item"))
                    {
                        scale *= 0.92f;
                    }
                    if (item.info.name.Equals("jacket.snow.item"))
                    {
                        scale *= 0.8f;
                    }
                    if (item.info.name.Equals("Hazmat_Suit.item"))
                    {
                        scale *= 0.5f;
                    }
                }
                if (player.IsSleeping())
                {
                    scale = 0.1f;
                    sleeping = "(sleeping)";
                }
                if (Instance.configData.Options.debug) Instance.Puts($"Player {player.displayName}{sleeping} total protection scale = {scale.ToString()}");
                return scale;
            }

            private void FixedUpdate()
            {
                float currentrain = Climate.GetRain(player.transform.position);
                float level = 0;
                float poison = 0;
                bool valid = false;

                if (player.metabolism.wetness.lastValue > 0 && !Instance.protectedPlayers.Contains(player.userID))
                {
                    float scale = CheckProtection();
                    if (Instance.configData.Options.debug) Instance.Puts($"Working on {player.UserIDString}, currentrain: {currentrain.ToString()}");

                    if (currentrain > 0.5f)
                    {
                        if (scale > 0)
                        {
                            valid = true;
                            level = Options.hilevelbump * scale;
                            poison = Options.hipoisonbump * scale;
                        }
                    }
                    else if (currentrain > 0)
                    {
                        if (scale > 0)
                        {
                            valid = true;
                            level = Options.lolevelbump * scale;
                            poison = Options.lopoisonbump * scale;
                        }
                    }
                }
                else if (player.metabolism.radiation_poison.lastValue > 0)
                {
                    valid = true;
                    level *= 0.9f;
                    poison *= 0.9f;
                }

                if (valid && (level > 0 || poison > 0))
                {
                    if (Instance.configData.Options.debug) Instance.Puts($"Increasing player radiation for {player.displayName} by (Level/Poison {level}/{poison})");
                    player.metabolism.radiation_level.SetValue(player.metabolism.radiation_level.lastValue + level);
                    player.metabolism.radiation_poison.SetValue(player.metabolism.radiation_poison.lastValue + poison);
                    if (!notified)
                    {
                        if (!player.IsSleeping())
                        {
                            Instance.SendReply(player, Instance.Lang("seekshelter", player.UserIDString));
                        }
                        NotifyTimer(true);
                        notified = true;
                    }
                }
            }
        }
        #endregion
    }
}
