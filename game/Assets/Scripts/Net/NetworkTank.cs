using FishNet.Component.Prediction;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using FishNet.Utility.Template;
using UnityEngine;

namespace Potshot.Net
{
    /// <summary>
    /// Server-authoritative predicted tank movement. The owner (or the
    /// server, for ownerless bots — IsController) samples input each tick;
    /// everyone steps the SAME TankMotor.Step in the replicate; the server
    /// reconciles pose+velocity+BoostState (the M1g contract). Pattern and
    /// signatures verbatim from FishNet 4.7.2 RigidbodyPrediction demo +
    /// codegen enforcement (M2b review).
    /// </summary>
    public class NetworkTank : TickNetworkBehaviour
    {
        public struct TankMoveData : IReplicateData
        {
            public Vector2 Move;
            public Vector3 AimWorldPos;
            public bool Fire;
            public bool Boost;

            uint _tick;
            public uint GetTick() => _tick;
            public void SetTick(uint value) => _tick = value;
            public void Dispose() { }
        }

        public struct TankReconcileData : IReconcileData
        {
            public RigidbodyState Rigidbody;
            public float BoostActiveLeft;
            public float BoostCooldownLeft;

            uint _tick;
            public uint GetTick() => _tick;
            public void SetTick(uint value) => _tick = value;
            public void Dispose() { }
        }

        TankController _controller;
        Rigidbody _rb;
        NetworkWeapon _weapon;

        public override void OnStartNetwork()
        {
            _controller = GetComponent<TankController>();
            _rb = GetComponent<Rigidbody>();
            _weapon = GetComponent<NetworkWeapon>();
            _controller.ExternallyDriven = true;
            SetTickCallbacks(TickCallback.Tick | TickCallback.PostTick);
        }

        public override void OnStopNetwork()
        {
            if (_controller != null) _controller.ExternallyDriven = false;
        }

        public override void OnStartClient()
        {
            if (IsOwner)
            {
                Debug.Log("[Net] owned tank spawned — session live");
                // Follow the SMOOTHED graphical child, never the 30 Hz-
                // stepped root — smoothing the camera against a stepped
                // target only attenuates the sawtooth (smoothing review).
                var follow = FindFirstObjectByType<CameraFollow>();
                if (follow != null)
                {
                    var graphical = transform.Find("Graphical");
                    follow.target = graphical != null ? graphical : transform;
                }
                return;
            }
            // Remote copies must not read the local keyboard or draw HUDs.
            Destroy(GetComponent<PlayerTankInput>());
            Destroy(GetComponent<PlaytestHotkeys>());
            Destroy(GetComponent<DevHud>());
        }

        protected override void TimeManager_OnTick()
        {
            PerformReplicate(BuildMoveData());
        }

        protected override void TimeManager_OnPostTick()
        {
            CreateReconcile();
        }

        TankMoveData BuildMoveData()
        {
            if (!IsController) return default;
            var source = _controller.InputSource;
            if (source == null) return default;

            var sample = source.Sample();
            // PostMatch freeze is read from the SYNCED lobby phase — the
            // owner builds inputs, so a server-side flag can't stop them.
            var lobby = FindFirstObjectByType<LobbyState>();
            bool frozen = _controller.InputFrozen
                || (lobby != null && lobby.CurrentPhase == LobbyState.Phase.PostMatch);
            if (frozen)
            {
                sample.Move = Vector2.zero;
                sample.Fire = false;
                sample.Boost = false;
            }
            return new TankMoveData
            {
                Move = sample.Move,
                AimWorldPos = sample.AimWorldPos,
                Fire = sample.Fire,
                Boost = sample.Boost,
            };
        }

        [Replicate]
        void PerformReplicate(TankMoveData data,
            ReplicateState state = ReplicateState.Invalid,
            Channel channel = Channel.Unreliable)
        {
            var sample = new TankInputSample
            {
                Move = data.Move,
                AimWorldPos = data.AimWorldPos,
                Fire = data.Fire,
                Boost = data.Boost,
            };
            _controller.SetNetworkSample(in sample);
            TankMotor.Step(_rb, in sample, _controller.spec,
                ref _controller.Boost, (float)TimeManager.TickDelta);
            // Fire only on fresh created ticks, never replays (owner fresh
            // = Ticked|Created; replays always carry Replayed — 4.7.2
            // source; IsReplayed() is obsolete, use ContainsReplayed()).
            bool freshTick = state.ContainsCreated() && !state.ContainsReplayed();
            if (freshTick && _weapon != null)
            {
                // Server: the real, damage-dealing shells (M2c).
                if (IsServerStarted)
                    _weapon.ServerTick(in sample, (float)TimeManager.TickDelta);
                // Owner (pure client): instant local tracer from the
                // on-screen barrel — the authoritative shell is hidden for
                // us (fire-feel).
                else if (IsOwner)
                    _weapon.OwnerVisualTick(in sample, (float)TimeManager.TickDelta);
            }
        }

        public override void CreateReconcile()
        {
            if (!IsServerStarted) return;
            PerformReconcile(new TankReconcileData
            {
                Rigidbody = _rb.GetState(),
                BoostActiveLeft = _controller.Boost.activeLeft,
                BoostCooldownLeft = _controller.Boost.cooldownLeft,
            });
        }

        [Reconcile]
        void PerformReconcile(TankReconcileData data,
            Channel channel = Channel.Unreliable)
        {
            _rb.SetState(data.Rigidbody);
            _controller.Boost = new TankMotor.BoostState
            {
                activeLeft = data.BoostActiveLeft,
                cooldownLeft = data.BoostCooldownLeft,
            };
        }
    }
}
