﻿// VRCP_TriggerHelper
// Created by CyanLaser

#if VRC_SDK_VRCSDK2

using System;
using System.Collections.Generic;
using UnityEngine;
using VRCSDK2;

namespace VRCPrefabs.CyanEmu
{
    [AddComponentMenu("")]
    public class VRCP_TriggerHelper : MonoBehaviour, VRCP_Interactable, VRCP_Pickupable, VRCP_StationHandler
    {
        private static readonly int PLAYER_LAYER = 1 << 9; // Player Layer

        public VRC_Trigger Trigger { get; private set; }
        public bool HasGlobalOnEnable { get; private set; }
        public bool HasGlobalOnDisable { get; private set; }

        private List<VRC_Trigger.TriggerEvent> enterTriggers = new List<VRC_Trigger.TriggerEvent>();
        private List<VRC_Trigger.TriggerEvent> exitTriggers = new List<VRC_Trigger.TriggerEvent>();
        private List<VRC_Trigger.TriggerEvent> enterCollider = new List<VRC_Trigger.TriggerEvent>();
        private List<VRC_Trigger.TriggerEvent> exitCollider = new List<VRC_Trigger.TriggerEvent>();
        private List<VRC_Trigger.TriggerEvent> particleCollider = new List<VRC_Trigger.TriggerEvent>();
        private List<VRC_Trigger.TriggerEvent> timerTriggers = new List<VRC_Trigger.TriggerEvent>();
        private List<VRC_Trigger.TriggerEvent> interactTriggers = new List<VRC_Trigger.TriggerEvent>();
        private List<VRC_Trigger.TriggerEvent> onKeyTriggers = new List<VRC_Trigger.TriggerEvent>();
        
        private bool hasObjectSync = false;
        private bool hasAddedListeners = false;


        public static void InitializeTrigger(VRC.SDKBase.VRC_Trigger trigger)
        {
            if (trigger.ExecuteTrigger == null)
            {
                trigger.ExecuteTrigger = new Action<VRC_Trigger.TriggerEvent>(VRCP_TriggerExecutor.ExecuteTrigger);
            }

            trigger.gameObject.AddComponent<VRCP_TriggerHelper>().SetTrigger(trigger as VRC_Trigger);
        }

        private void SetTrigger(VRC_Trigger trigger)
        {
            if (GetComponents<VRC_Trigger>().Length > 1)
            {
                this.LogError("Objet contains more than one VRC_Trigger component! " + VRCP_Utils.PathForObject(gameObject));
            }

            if (trigger == null)
            {
                this.LogError("Trigger is null. Destroying helper.");
                DestroyImmediate(this);
                return;
            }

            Trigger = trigger;

            VRCP_TriggerExecutor.AddTrigger(Trigger);

            hasObjectSync = GetComponent<VRC_ObjectSync>();

            VRC_CombatSystem combatSystem = FindObjectOfType<VRC_CombatSystem>();

            // Go through and make sure all null targets reference itself.
            for (int trig = 0; trig < Trigger.Triggers.Count; ++trig)
            {
                VRC_Trigger.TriggerEvent trigEvent = Trigger.Triggers[trig];
                for (int trigEventInd = 0; trigEventInd < trigEvent.Events.Count; ++trigEventInd)
                {
                    VRC_EventHandler.VrcEvent vrcEvent = trigEvent.Events[trigEventInd];
                    GameObject obj = gameObject;
                    bool isCombat = false;
                    if (
                        (vrcEvent.EventType == VRC_EventHandler.VrcEventType.AddDamage ||
                        vrcEvent.EventType == VRC_EventHandler.VrcEventType.AddHealth) &&
                        combatSystem != null
                    )
                    {
                        obj = combatSystem.gameObject;
                        isCombat = true;
                    }

                    if (vrcEvent.ParameterObjects == null || vrcEvent.ParameterObjects.Length == 0)
                    {
                        if (vrcEvent.ParameterObject != null)
                        {
                            obj = vrcEvent.ParameterObject;
                        }
                        vrcEvent.ParameterObjects = new GameObject[] { obj };
                        this.LogWarning("VRC_Trigger[" + trig + "][" + trigEventInd + "] has no objects. Setting it to target itself. " + VRC.Tools.GetGameObjectPath(obj));
                    }
                    else
                    {
                        bool found = false;
                        for (int i = 0; i < vrcEvent.ParameterObjects.Length; ++i)
                        {
                            if (vrcEvent.ParameterObjects[i] == null)
                            {
                                vrcEvent.ParameterObjects[i] = obj;
                                found = true;
                            }
                        }

                        if (found && !isCombat)
                        {
                            this.LogWarning("VRC_Trigger[" + trig + "][" + trigEventInd + "] has null targets. Setting targets to itself. " + VRC.Tools.GetGameObjectPath(obj));
                        }
                    }
                }
            }



            for (int i = 0; i < Trigger.Triggers.Count; ++i)
            {
                if (Trigger.Triggers[i].TriggerType == VRC_Trigger.TriggerType.OnEnterTrigger)
                {
                    enterTriggers.Add(Trigger.Triggers[i]);
                    CheckForPlayerLayerOverSync(Trigger.Triggers[i]);
                }
                else if (Trigger.Triggers[i].TriggerType == VRC_Trigger.TriggerType.OnExitTrigger)
                {
                    exitTriggers.Add(Trigger.Triggers[i]);
                    CheckForPlayerLayerOverSync(Trigger.Triggers[i]);
                }
                else if (Trigger.Triggers[i].TriggerType == VRC_Trigger.TriggerType.OnEnterCollider)
                {
                    enterCollider.Add(Trigger.Triggers[i]);
                    CheckForPlayerLayerOverSync(Trigger.Triggers[i]);
                }
                else if (Trigger.Triggers[i].TriggerType == VRC_Trigger.TriggerType.OnExitCollider)
                {
                    exitCollider.Add(Trigger.Triggers[i]);
                    CheckForPlayerLayerOverSync(Trigger.Triggers[i]);
                }
                else if (Trigger.Triggers[i].TriggerType == VRC_Trigger.TriggerType.OnTimer)
                {
                    Trigger.ResetClock(Trigger.Triggers[i]);
                    timerTriggers.Add(Trigger.Triggers[i]);

                    if (Trigger.Triggers[i].BroadcastType.IsAlwaysBufferedBroadcastType())
                    {
                        HasGlobalOnEnable = true;
                    }
                }
                else if (Trigger.Triggers[i].TriggerType == VRC_Trigger.TriggerType.OnInteract)
                {
                    interactTriggers.Add(Trigger.Triggers[i]);
                }
                else if (Trigger.Triggers[i].TriggerType == VRC_Trigger.TriggerType.OnKeyDown || Trigger.Triggers[i].TriggerType == VRC_Trigger.TriggerType.OnKeyUp)
                {
                    onKeyTriggers.Add(Trigger.Triggers[i]);
                }
                else if (Trigger.Triggers[i].TriggerType == VRC_Trigger.TriggerType.OnParticleCollision)
                {
                    particleCollider.Add(Trigger.Triggers[i]);
                }
                else if (Trigger.Triggers[i].TriggerType == VRC_Trigger.TriggerType.OnPlayerJoined)
                {
                    if (Trigger.Triggers[i].BroadcastType.IsAlwaysBufferedBroadcastType())
                    {
                        this.LogWarning("Oversync on player joined! " + Trigger.Triggers[i].GetTriggerEventAsString());
                    }
                }
                else if (Trigger.Triggers[i].TriggerType == VRC_Trigger.TriggerType.OnPlayerLeft)
                {
                    if (Trigger.Triggers[i].BroadcastType.IsAlwaysBufferedBroadcastType())
                    {
                        this.LogWarning("Oversync on player left! " + Trigger.Triggers[i].GetTriggerEventAsString());
                    }
                }
                else if (Trigger.Triggers[i].TriggerType == VRC_Trigger.TriggerType.OnEnable)
                {
                    if (Trigger.Triggers[i].BroadcastType.IsAlwaysBufferedBroadcastType())
                    {
                        HasGlobalOnEnable = true;
                    }
                }
                else if (Trigger.Triggers[i].TriggerType == VRC_Trigger.TriggerType.OnDisable)
                {
                    if (Trigger.Triggers[i].BroadcastType.IsAlwaysBufferedBroadcastType())
                    {
                        HasGlobalOnDisable = true;
                    }
                }
            }

            if (enterTriggers.Count + exitTriggers.Count + enterCollider.Count + exitCollider.Count + timerTriggers.Count + onKeyTriggers.Count + particleCollider.Count == 0)
            {
                enabled = false;
            }

            AddListeners();
        }

        private void CheckForPlayerLayerOverSync(VRC_Trigger.TriggerEvent triggerEvent)
        {
            if ((triggerEvent.Layers & PLAYER_LAYER) != 0 && triggerEvent.BroadcastType.IsEveryoneBroadcastType())
            {
                this.LogWarning("Player layer Enter/Exit trigger Oversync! " + triggerEvent.GetTriggerEventAsString());
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            HandleCollisionEvent(other.gameObject, enterTriggers);
        }

        private void OnTriggerExit(Collider other)
        {
            HandleCollisionEvent(other.gameObject, exitTriggers);
        }

        private void OnCollisionEnter(Collision collision)
        {
            HandleCollisionEvent(collision.gameObject, enterCollider);
        }

        private void OnCollisionExit(Collision collision)
        {
            HandleCollisionEvent(collision.gameObject, exitCollider);
        }

        private void OnParticleCollision(GameObject other)
        {
            HandleCollisionEvent(other, particleCollider);
        }

        private void HandleCollisionEvent(GameObject other, List<VRC_Trigger.TriggerEvent> events)
        {
            if (!Trigger.enabled)
            {
                return;
            }

            bool synced = other.GetComponent<VRC_ObjectSync>() != null || hasObjectSync;

            int layer = 1 << other.gameObject.layer;

            foreach (VRC_Trigger.TriggerEvent evt in events)
            {
                if ((evt.Layers & layer) != 0)
                {
                    Trigger.ExecuteTrigger(evt);
                    
                    if (synced && evt.BroadcastType.IsEveryoneBroadcastType())
                    {
                        this.LogWarning("Potential ObjectSync Enter/Exit trigger Oversync! " + evt.GetTriggerEventAsString());
                    }
                }
            }
        }

        // TODO optimize
        public void UpdateTimers(List<VRC_Trigger.TriggerEvent> eventsToFire)
        {
            if (!gameObject.activeInHierarchy || !Trigger.enabled)
            {
                return;
            }

            foreach (VRC_Trigger.TriggerEvent timer in timerTriggers)
            {
                if (timer.EventFired)
                {
                    continue;
                }

                timer.Timer += Time.deltaTime;

                if (timer.Timer >= timer.Duration)
                {
                    eventsToFire.Add(timer);
                    timer.EventFired = true;

                    if (timer.Repeat)
                    {
                        Trigger.ResetClock(timer);
                    }
                }
            }
        }

        // TODO optimize 
        public void UpdateOnKeyTriggers(List<VRC_Trigger.TriggerEvent> eventsToFire)
        {
            if (!gameObject.activeInHierarchy || !Trigger.enabled)
            {
                return;
            }

            foreach (VRC_Trigger.TriggerEvent keyEvent in onKeyTriggers)
            {
                bool active = false;
                if (keyEvent.TriggerType == VRC_Trigger.TriggerType.OnKeyDown)
                {
                    active = Input.GetKeyDown(keyEvent.Key);
                } else
                {
                    active = Input.GetKeyUp(keyEvent.Key);
                }
                
                if (active)
                {
                    eventsToFire.Add(keyEvent);
                }
            }
        }

        private void OnEnable()
        {
            if (Trigger == null)
            {
                return;
            }
            AddListeners();
        }

        private void OnDisable()
        {
            if (Trigger == null)
            {
                return;
            }
            RemoveListeners();
        }

        private void AddListeners()
        {
            if (hasAddedListeners)
            {
                return;
            }
            hasAddedListeners = true;

            if (onKeyTriggers.Count > 0)
            {
                VRCP_TriggerExecutor.AddKeyTrigger(this);
            }

            if (timerTriggers.Count > 0)
            {
                VRCP_TriggerExecutor.AddTimerTrigger(this);
            }
        }

        private void RemoveListeners()
        {
            if (!hasAddedListeners)
            {
                return;
            }
            hasAddedListeners = false;

            if (onKeyTriggers.Count > 0)
            {
                VRCP_TriggerExecutor.RemoveKeyTrigger(this);
            }

            if (timerTriggers.Count > 0)
            {
                VRCP_TriggerExecutor.RemoveTimerTrigger(this);
            }
        }

        private void OnDestroy()
        {
            VRCP_TriggerExecutor.RemoveTrigger(Trigger);
        }

#region VRCP_Interactable

        public bool CanInteract(float distance)
        {
            return Trigger.enabled && interactTriggers.Count > 0 && Trigger.proximity >= distance;
        }

        public string GetInteractText()
        {
            return Trigger.interactText;
        }

        public void Interact()
        {
            Trigger.Interact();
        }

#endregion

#region VRCP_Pickupable

        public void OnPickup()
        {
            Trigger.ExecuteTriggerType(VRC_Trigger.TriggerType.OnPickup);
        }

        public void OnDrop()
        {
            Trigger.ExecuteTriggerType(VRC_Trigger.TriggerType.OnDrop);
        }

        public void OnPickupUseDown()
        {
            Trigger.ExecuteTriggerType(VRC_Trigger.TriggerType.OnPickupUseDown);
        }

        public void OnPickupUseUp()
        {
            Trigger.ExecuteTriggerType(VRC_Trigger.TriggerType.OnPickupUseUp);
        }

#endregion

#region VRCP_StationHandler

        public void OnStationEnter(VRC.SDKBase.VRCStation station)
        {
            VRC_Trigger.TriggerCustom((station as VRCSDK2.VRC_Station).OnLocalPlayerEnterStation);
            Trigger.ExecuteTriggerType(VRC_Trigger.TriggerType.OnStationEntered);
        }

        public void OnStationExit(VRC.SDKBase.VRCStation station)
        {
            VRC_Trigger.TriggerCustom((station as VRCSDK2.VRC_Station).OnLocalPlayerExitStation);
            Trigger.ExecuteTriggerType(VRC_Trigger.TriggerType.OnStationExited);
        }

#endregion
    }
}
#endif