//#define DEBUG
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Acid Rain", "RFC1920", "1.0.1", ResourceId = 1160)]
    [Description("The rain can kill you - take cover!")]

    class AcidRain : RustPlugin
    {
        private ConfigData configData;
        public static AcidRain Instance = null;
        public List<ulong> protectedPlayers = new List<ulong>();

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
                ["beware"] = "Beware of the ACID RAIN!",
                ["protected"] = "You have {0} minutes to protect yourself from the ACID RAIN!",
                ["recovered"] = "You have recovered from the ACID RAIN."
            }, this);
        }

        private void Loaded()
        {
            Instance = this;
            LoadConfigVariables();
            foreach (var pl in BasePlayer.activePlayerList)
            {
                if (!pl.gameObject.GetComponent<AcidRads>())
                {
                    pl.gameObject.AddComponent<AcidRads>();
                    var c = pl.GetComponent<AcidRads>();
                    c.Options = new Options
                    {
                        lolevelbump = configData.Options.lolevelbump,
                        lopoisonbump = configData.Options.lopoisonbump,
                        hilevelbump = configData.Options.hilevelbump,
                        hipoisonbump = configData.Options.hipoisonbump,
                        notifyTimer = configData.Options.notifyTimer
                    };
                }
            }
        }

        private void OnServerShutdown() => Unload();
        private void Unload()
        {
            foreach (var pl in BasePlayer.activePlayerList)
            {
                if(pl.GetComponent<AcidRads>())
                {
                    UnityEngine.Object.Destroy(pl.gameObject.GetComponent<AcidRads>());
                }
            }
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (!player.gameObject.GetComponent<AcidRads>())
            {
                player.gameObject.AddComponent<AcidRads>();
                var c = player.GetComponent<AcidRads>();
                c.Options = new Options
                {
                    lolevelbump = configData.Options.lolevelbump,
                    lopoisonbump = configData.Options.lopoisonbump,
                    hilevelbump = configData.Options.hilevelbump,
                    hipoisonbump = configData.Options.hipoisonbump,
                    notifyTimer = configData.Options.notifyTimer
                };
            }
            if (!protectedPlayers.Contains(player.userID))
            {
                SendReply(player, Instance.Lang("beware", player.UserIDString));
            }
        }

        void OnUserRespawned(IPlayer player)
        {
            Message(player, "protected");
            protectedPlayers.Add((player.Object as BasePlayer).userID);
            timer.Once(configData.Options.protectionTimer, () =>
            {
                protectedPlayers.Remove((player.Object as BasePlayer).userID);
                Message(player, "beware");
            });
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (player.gameObject.GetComponent<AcidRads>())
            {
                UnityEngine.Object.Destroy(player.gameObject.GetComponent<AcidRads>());
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
            var config = new ConfigData
            {
                Version = Version,
            };
            config.Options.hilevelbump = 1f;
            config.Options.hipoisonbump = 0.5f;
            config.Options.lolevelbump = 0.2f;
            config.Options.lopoisonbump = 0.1f;
            config.Options.notifyTimer = 60f;
            config.Options.protectionTimer = 300f;

            SaveConfig(config);
        }
        private void SaveConfig(ConfigData config)
        {
            Config.WriteObject(config, true);
        }
        #endregion

        #region classes
        private class ConfigData
        {
            public Options Options = new Options();
            public VersionNumber Version;
        }

        private class Options
        {
            public float hilevelbump;
            public float hipoisonbump;
            public float lolevelbump;
            public float lopoisonbump;
            public float notifyTimer;
            public float protectionTimer;
        }

        class AcidRads : MonoBehaviour
        {
            public Options Options = new Options();
            private Timer timer;

            private BasePlayer player;
            private bool notified = false;

            private void Awake() => player = GetComponentInParent<BasePlayer>();
            private void OnDestroy() => timer.Destroy();

            private void NotifyTimer(bool start = false)
            {
                if(timer != null && timer.Destroyed)
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
                float scale = 1f;
                foreach (var item in player.inventory.containerWear.itemList)
                {
#if DEBUG
                    Instance.Puts($"Player {player.displayName} wearing {item.info.name}");
#endif
                    if (item.info.name.Contains("hat") || item.info.name.Contains("cap"))
                    {
                        scale *= 0.99f;
                    }
                    if (item.info.name.Contains("shirt") || item.info.name.Contains("short"))
                    {
                        scale *= 0.98f;
                    }
                    if(item.info.name.Contains("Boot"))
                    {
                        scale *= 0.97f;
                    }
                    if (item.info.name.Contains("gloves"))
                    {
                        scale *= 0.96f;
                    }
                    else if(item.info.name.Contains("pants"))
                    {
                        scale *= 0.95f;
                    }
                    if (item.info.name.Contains("poncho"))
                    {
                        scale *= 0.92f;
                    }
                    if (item.info.name.Contains("jacket.snow"))
                    {
                        scale *= 0.8f;
                    }
                    if(item.info.name.Contains("azmat"))
                    {
                        scale *= 0.5f;
                    }
#if DEBUG
                    Instance.Puts($"Player {player.displayName} total protection scale = {scale.ToString()}");
#endif
                }
                return scale;
            }

            void FixedUpdate()
            {
                var currentrain = Climate.GetRain(player.transform.position);
                float level = 0;
                float poison = 0;
                bool valid = false;

                if (player.metabolism.wetness.lastValue > 0 && !Instance.protectedPlayers.Contains(player.userID))
                {
                    float scale = CheckProtection();
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
                else if(player.metabolism.radiation_poison.lastValue > 0)
                {
                    valid = true;
                    level *= 0.9f;
                    poison *= 0.9f;
                }

                if (valid && (level > 0 || poison > 0))
                {
#if DEBUG
                    Instance.Puts($"Increasing player radiation for {player.displayName} by (Level/Poison {level}/{poison})");
#endif
                    player.metabolism.radiation_level.SetValue(player.metabolism.radiation_level.lastValue + level);
                    player.metabolism.radiation_poison.SetValue(player.metabolism.radiation_poison.lastValue + poison);
                    if (!notified)
                    {
                        Instance.SendReply(player, Instance.Lang("seekshelter", player.UserIDString));
                        NotifyTimer(true);
                        notified = true;
                    }
                }
            }
        }
        #endregion
    }
}
