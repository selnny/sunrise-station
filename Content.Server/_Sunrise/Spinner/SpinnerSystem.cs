using Content.Shared._Sunrise.Spinner;
using Content.Shared.Interaction;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.GameStates;
using Robust.Shared.Maths;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;
using Content.Shared.Verbs;
using Content.Shared.Interaction.Events;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics;

namespace Content.Server._Sunrise.Spinner
{
    public sealed partial class SpinnerSystem : EntitySystem
    {
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly SharedTransformSystem _xform = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<SpinnerComponent, ActivateInWorldEvent>(OnActivateInWorld);
            SubscribeLocalEvent<SpinnerComponent, GetVerbsEvent<InteractionVerb>>(AddSpinVerb);
            SubscribeLocalEvent<SpinnerComponent, ComponentInit>(OnInit);
            SubscribeLocalEvent<SpinnerComponent, ComponentShutdown>(OnShutdown);
        }

        private void OnInit(EntityUid uid, SpinnerComponent comp, ComponentInit args)
        {

        }

        private void OnShutdown(EntityUid uid, SpinnerComponent comp, ComponentShutdown args)
        {
            comp.IsSpinning = false;
            comp.RemainingSeconds = 0f;
            comp.CurrentDegPerSec = 0f;
        }

        private void OnActivateInWorld(EntityUid uid, SpinnerComponent comp, ActivateInWorldEvent args)
        {
            if (args.Handled)
                return;

            if (!TryComp<PhysicsComponent>(uid, out var physics) || physics.BodyType != BodyType.Static)
                return;

            ToggleSpin(uid, comp);
            args.Handled = true;
        }

        private void AddSpinVerb(EntityUid uid, SpinnerComponent comp, GetVerbsEvent<InteractionVerb> args)
        {
            if (!args.CanAccess || !args.CanInteract)
                return;

            if (!TryComp<PhysicsComponent>(uid, out var physics) || physics.BodyType != BodyType.Static)
                return;

            InteractionVerb verb = new()
            {
                Act = () => ToggleSpin(uid, comp),
                Text = comp.IsSpinning ? "Остановить вращение" : "Крутить",
            };
            args.Verbs.Add(verb);
        }

        private void ToggleSpin(EntityUid uid, SpinnerComponent comp)
        {
            if (comp.IsSpinning)
            {
                comp.RemainingSeconds = 0f;
                comp.IsSpinning = false;
                Dirty(uid, comp);
                return;
            }

            StartSpin(uid, comp);
        }

        private void StartSpin(EntityUid uid, SpinnerComponent comp)
        {
            var seconds = _random.NextFloat(comp.MinSpinSeconds, comp.MaxSpinSeconds);
            var degPerSec = _random.NextFloat(comp.MinDegPerSec, comp.MaxDegPerSec);

            comp.IsSpinning = true;
            comp.RemainingSeconds = seconds;
            comp.CurrentDegPerSec = degPerSec;

            Dirty(uid, comp);
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            var query = EntityQueryEnumerator<SpinnerComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var comp, out var xform))
            {
                if (!comp.IsSpinning)
                    continue;

                var dt = frameTime;

                // вращаем вокруг Z (в 2D)
                var deltaDeg = comp.CurrentDegPerSec * dt;

                var newAngle = xform.LocalRotation.Degrees + deltaDeg;
                _xform.SetLocalRotation(uid, Angle.FromDegrees(newAngle));

                comp.RemainingSeconds -= dt;

                if (comp.RemainingSeconds <= 0f)
                {
                    comp.CurrentDegPerSec *= comp.BrakeFactor;
                    if (MathF.Abs(comp.CurrentDegPerSec) < 10f)
                    {
                        comp.IsSpinning = false;
                        comp.CurrentDegPerSec = 0f;
                        comp.RemainingSeconds = 0f;
                        Dirty(uid, comp);
                        continue;
                    }
                }

                if (comp.RemainingSeconds > 0f && comp.RemainingSeconds < 0.5f)
                    comp.CurrentDegPerSec *= 0.995f;

                Dirty(uid, comp);
            }
        }
    }
}
