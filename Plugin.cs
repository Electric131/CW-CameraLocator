
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Photon.Pun;
using ShopUtils;
using UnityEngine;
using Zorro.Core.Serizalization;

namespace CW_CameraLocator
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    [BepInDependency("hyydsz-ShopUtils")]
    public class Plugin : BaseUnityPlugin
    {
        public const string ModGUID = "Electric131.CameraLocator";
        public const string ModName = "CameraLocator";
        public const string ModVersion = "1.0.1";

        public static ManualLogSource? logger;

        public static Item? locatorItem;
        public static GameObject? locatorObject;
        public static AudioClip? pingSound;
        public static SFX_Instance? sfxInstance;

        private void Awake()
        {
            logger = Logger;
            logger.LogInfo($"Plugin {ModGUID} loaded, preparing to load resources!");

            AssetBundle data = AssetBundle.LoadFromMemory(Properties.Resources.cameralocator);
            locatorItem = data.LoadAsset<Item>("Assets/CameraLocatorItem.asset");
            locatorObject = data.LoadAsset<GameObject>("Assets/CameraLocatorObject.prefab");
            sfxInstance = data.LoadAsset<SFX_Instance>("Assets/CameraLocator-SFX.asset");
            locatorObject.AddComponent<ItemInstance>();
            locatorObject.AddComponent<CameraLocatorBehavior>();

            logger.LogInfo($"Assets loaded, registering item with game");

            Items.RegisterShopItem(locatorItem);

            logger.LogInfo($"Item loaded and registered successfully");
        }

        public class CameraLocatorBehavior : ItemInstanceBehaviour
        {
            private BatteryEntry? batteryEntry;
            private OnOffEntry? onOffEntry;
            private TimeEntry? lastPingEntry;

            private TMPro.TMP_Text? screenText;

            public float maxCharge = 30f;

            public enum LocatorState
            {
                OFF,
                NO_SIGNAL,
                LOCATED
            }

            public void UpdateScreenText(LocatorState state)
            {
                if (!screenText) return; // No text loaded, so what do we write to??
                switch (state)
                {
                    case LocatorState.OFF:
                        screenText.text = "";
                        break;
                    case LocatorState.NO_SIGNAL:
                        screenText.text = "No Signal";
                        screenText.color = Color.red;
                        break;
                    case LocatorState.LOCATED:
                        screenText.text = "Signal Located";
                        screenText.color = Color.green;
                        break;
                }
            }

            public override void ConfigItem(ItemInstanceData data, PhotonView playerView)
            {
                if (!data.TryGetEntry(out batteryEntry))
                {
                    batteryEntry = new BatteryEntry
                    {
                        m_charge = maxCharge,
                        m_maxCharge = maxCharge
                    };
                    data.AddDataEntry(batteryEntry);
                }
                if (!data.TryGetEntry(out onOffEntry))
                {
                    onOffEntry = new OnOffEntry
                    {
                        on = false
                    };
                    data.AddDataEntry(onOffEntry);
                }
                if (!data.TryGetEntry(out lastPingEntry))
                {
                    lastPingEntry = new TimeEntry // TimeEntry stores a single float, so I'm going to abuse it
                    {
                        currentTime = 0
                    };
                    data.AddDataEntry(lastPingEntry);
                }
                // Find components of the locator
                screenText = transform.Find("Physical/Screen/ScreenText").gameObject.GetComponent<TMPro.TMP_Text>();
            }

            public void Update()
            {
                if (isHeldByMe && !Player.localPlayer.HasLockedInput() && Player.localPlayer.input.clickWasPressed)
                {
                    onOffEntry.on = !onOffEntry.on;
                    onOffEntry.SetDirty();
                }
                if (onOffEntry.on && batteryEntry.m_charge > 0f)
                {
                    batteryEntry.m_charge -= Time.deltaTime;
                    ItemInstance[] items = FindObjectsOfType<ItemInstance>();
                    GameObject? closestCamera = null;
                    float closestDistance = 50f;
                    foreach (ItemInstance item in items)
                    {
                        if (!(item.name == "Camera1(Clone)" || item.name == "BrokenCamera(Clone)")) continue;
                        float distance = Vector3.Distance(item.gameObject.transform.position, transform.position);
                        if (distance >= closestDistance) continue;
                        closestCamera = item.gameObject;
                        closestDistance = distance;
                    }
                    if (closestCamera != null)
                    {
                        UpdateScreenText(LocatorState.LOCATED);
                        lastPingEntry.currentTime += Time.deltaTime;
                        // Formula to calculate time between each ping (0.0023x^1.8 + 0.4)
                        float timeBetweenPings = Mathf.Max(0.0023f * Mathf.Pow(closestDistance, 1.8f) + 0.4f, 0.5f);
                        if (closestDistance < 8) // Overload time between pings if close enough, to make it go a bit haywire
                        {
                            timeBetweenPings = 0.2f;
                        }
                        if (lastPingEntry.currentTime > timeBetweenPings)
                        {
                            sfxInstance.Play(transform.position, false, 1f, null);
                            lastPingEntry.currentTime = 0f;
                        }
                    } else
                    {
                        UpdateScreenText(LocatorState.NO_SIGNAL);
                        lastPingEntry.currentTime = 0f;
                    }
                } else
                {
                    UpdateScreenText(LocatorState.OFF);
                    lastPingEntry.currentTime = 0f;
                }
            }
        }
    }
}
