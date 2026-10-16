using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Demons
{
    public enum PebbleState : byte { OnBelt, Held, OnStation, Floor, Flying }

    public class Pebble : NetworkBehaviour
    {
        public static readonly List<Pebble> All = new();

        public NetworkVariable<PebbleState> State = new();
        public NetworkVariable<ulong> Anchor = new(Net.None);
        public NetworkVariable<Vector3> FloorPos = new();
        public NetworkVariable<byte> Stamps = new();
        public NetworkVariable<float> BeltFrom = new();
        public NetworkVariable<double> BeltSince = new();
        public NetworkVariable<double> LastTouched = new();
        public NetworkVariable<int> LastHolder = new(-1);

        [SerializeField] Transform body;
        [SerializeField] Renderer[] stampDots;
        [SerializeField] GameObject wings;

        double flyStart;

        double Now => NetworkManager.ServerTime.Time;
        float BeltDistance => BeltFrom.Value + (float)(Now - BeltSince.Value) * Balance.I.beltSpeed;
        bool Idle => State.Value is PebbleState.Floor or PebbleState.OnStation;
        double ForgetIn => LastTouched.Value + Balance.I.forgetTime - Now;
        public bool IsGrabbable => State.Value is PebbleState.OnBelt or PebbleState.Floor or PebbleState.OnStation;

        public override void OnNetworkSpawn()
        {
            All.Add(this);
            Stamps.OnValueChanged += (_, _) => Paint();
            Paint();
        }

        public override void OnNetworkDespawn() => All.Remove(this);

        void Update()
        {
            if (IsServer) ServerTick();
            Place();
            Animate();
        }

        void ServerTick()
        {
            var b = Balance.I;
            if (State.Value == PebbleState.Flying)
            {
                if (Time.timeAsDouble - flyStart > b.flyAwayTime) NetworkObject.Despawn();
                return;
            }
            if (State.Value == PebbleState.OnBelt && BeltDistance >= Belt.Instance.Length) GameState.Instance.Deliver(this);
            else if (Idle && ForgetIn <= 0) FlyAway();
        }

        void Place()
        {
            switch (State.Value)
            {
                case PebbleState.OnBelt:
                    transform.position = Belt.Instance.PointAt(BeltDistance);
                    break;
                case PebbleState.Held when Net.Find<Demon>(Anchor.Value) is { } demon:
                    transform.position = demon.Hand.position;
                    break;
                case PebbleState.OnStation when Net.Find<Station>(Anchor.Value) is { } station:
                    transform.position = station.Slot.position;
                    break;
                case PebbleState.Floor:
                    transform.position = FloorPos.Value;
                    break;
                case PebbleState.Flying:
                    transform.position += new Vector3(0.5f, 2.5f, 0.3f) * Time.deltaTime;
                    break;
            }
        }

        void Animate()
        {
            bool flying = State.Value == PebbleState.Flying;
            wings.SetActive(flying);
            if (flying) wings.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.time * 30) * 35);

            body.localPosition = Idle && ForgetIn < Balance.I.warnTime
                ? new Vector3(Mathf.Sin(Time.time * 40) * 0.04f, 0, 0)
                : Vector3.zero;
        }

        void Paint()
        {
            for (int slot = 0; slot < stampDots.Length; slot++)
            {
                bool on = (Stamps.Value & (1 << slot)) != 0;
                stampDots[slot].gameObject.SetActive(on);
                if (on) stampDots[slot].material.color = Demon.SlotColors[slot];
            }
        }

        public void Hold(Demon demon)
        {
            Release();
            State.Value = PebbleState.Held;
            Anchor.Value = demon.NetworkObjectId;
            LastHolder.Value = demon.Slot.Value;
            demon.Carrying.Value = NetworkObjectId;
        }

        public void PutOnBelt(float distance)
        {
            Release();
            BeltFrom.Value = distance;
            BeltSince.Value = Now;
            State.Value = PebbleState.OnBelt;
        }

        public void PutOn(Station station)
        {
            Release();
            State.Value = PebbleState.OnStation;
            Anchor.Value = station.NetworkObjectId;
            station.Item.Value = NetworkObjectId;
            Touch();
        }

        public void Drop(Vector3 at)
        {
            Release();
            FloorPos.Value = new Vector3(at.x, 0.25f, at.z);
            State.Value = PebbleState.Floor;
            Touch();
        }

        public void Touch() => LastTouched.Value = Now;

        public void Consume()
        {
            Release();
            NetworkObject.Despawn();
        }

        void FlyAway()
        {
            Release();
            State.Value = PebbleState.Flying;
            flyStart = Time.timeAsDouble;
            GameState.Instance.Forgotten();
        }

        void Release()
        {
            if (State.Value == PebbleState.Held && Net.Find<Demon>(Anchor.Value) is { } demon)
                demon.Carrying.Value = Net.None;
            if (State.Value == PebbleState.OnStation && Net.Find<Station>(Anchor.Value) is { } station)
                station.Clear();
            Anchor.Value = Net.None;
        }
    }
}
