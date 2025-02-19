// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Input.StateChanges.Events;
using osu.Framework.Input.States;
using osu.Game.Configuration;
using osu.Game.Input;
using osu.Game.Input.Bindings;
using osu.Game.Input.Handlers;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play.HUD;
using osu.Game.Screens.Play.HUD.ClicksPerSecond;
using static osu.Game.Input.Handlers.ReplayInputHandler;

namespace osu.Game.Rulesets.UI
{
    public abstract partial class RulesetInputManager<T> : PassThroughInputManager, ICanAttachHUDPieces, IHasReplayHandler, IHasRecordingHandler
        where T : struct
    {
        protected override bool AllowRightClickFromLongTouch => false;

        public readonly KeyBindingContainer<T> KeyBindingContainer;

        [Resolved(CanBeNull = true)]
        private ScoreProcessor scoreProcessor { get; set; }

        private ReplayRecorder recorder;

        public ReplayRecorder Recorder
        {
            set
            {
                if (value == recorder)
                    return;

                if (value != null && recorder != null)
                    throw new InvalidOperationException("Cannot attach more than one recorder");

                recorder?.Expire();
                recorder = value;

                if (recorder != null)
                    KeyBindingContainer.Add(recorder);
            }
        }

        protected override InputState CreateInitialState() => new RulesetInputManagerInputState<T>(base.CreateInitialState());

        protected override Container<Drawable> Content => content;

        private readonly Container content;

        protected RulesetInputManager(RulesetInfo ruleset, int variant, SimultaneousBindingMode unique)
        {
            InternalChild = KeyBindingContainer =
                CreateKeyBindingContainer(ruleset, variant, unique)
                    .WithChild(content = new Container { RelativeSizeAxes = Axes.Both });
        }

        [BackgroundDependencyLoader(true)]
        private void load(OsuConfigManager config)
        {
            mouseDisabled = config.GetBindable<bool>(OsuSetting.MouseDisableButtons);
            tapsDisabled = config.GetBindable<bool>(OsuSetting.TouchDisableGameplayTaps);
        }

        #region Action mapping (for replays)

        public override void HandleInputStateChange(InputStateChangeEvent inputStateChange)
        {
            switch (inputStateChange)
            {
                case ReplayStateChangeEvent<T> stateChangeEvent:
                    foreach (var action in stateChangeEvent.ReleasedActions)
                        KeyBindingContainer.TriggerReleased(action);

                    foreach (var action in stateChangeEvent.PressedActions)
                        KeyBindingContainer.TriggerPressed(action);
                    break;

                case ReplayStatisticsFrameEvent statisticsStateChangeEvent:
                    scoreProcessor?.ResetFromReplayFrame(statisticsStateChangeEvent.Frame);
                    break;

                default:
                    base.HandleInputStateChange(inputStateChange);
                    break;
            }
        }

        #endregion

        #region IHasReplayHandler

        private ReplayInputHandler replayInputHandler;

        public ReplayInputHandler ReplayInputHandler
        {
            get => replayInputHandler;
            set
            {
                if (replayInputHandler != null) RemoveHandler(replayInputHandler);

                replayInputHandler = value;
                UseParentInput = replayInputHandler == null;

                if (replayInputHandler != null)
                    AddHandler(replayInputHandler);
            }
        }

        #endregion

        #region Setting application (disables etc.)

        private Bindable<bool> mouseDisabled;
        private Bindable<bool> tapsDisabled;

        protected override bool Handle(UIEvent e)
        {
            switch (e)
            {
                case MouseDownEvent:
                    if (mouseDisabled.Value)
                        return true; // importantly, block upwards propagation so global bindings also don't fire.

                    break;

                case MouseUpEvent mouseUp:
                    if (!CurrentState.Mouse.IsPressed(mouseUp.Button))
                        return false;

                    break;
            }

            return base.Handle(e);
        }

        protected override bool HandleMouseTouchStateChange(TouchStateChangeEvent e)
        {
            if (tapsDisabled.Value)
            {
                // Only propagate positional data when taps are disabled.
                e = new TouchStateChangeEvent(e.State, e.Input, e.Touch, false, e.LastPosition);
            }

            return base.HandleMouseTouchStateChange(e);
        }

        #endregion

        #region Key Counter Attachment

        public void Attach(InputCountController inputCountController)
        {
            var triggers = KeyBindingContainer.DefaultKeyBindings
                                              .Select(b => b.GetAction<T>())
                                              .Distinct()
                                              .Select(action => new KeyCounterActionTrigger<T>(action))
                                              .ToArray();

            KeyBindingContainer.AddRange(triggers);
            inputCountController.AddRange(triggers);
        }

        #endregion

        #region Keys per second Counter Attachment

        public void Attach(ClicksPerSecondController controller) => KeyBindingContainer.Add(new ActionListener(controller));

        private partial class ActionListener : Component, IKeyBindingHandler<T>
        {
            private readonly ClicksPerSecondController controller;

            public ActionListener(ClicksPerSecondController controller)
            {
                this.controller = controller;
            }

            public bool OnPressed(KeyBindingPressEvent<T> e)
            {
                controller.AddInputTimestamp();
                return false;
            }

            public void OnReleased(KeyBindingReleaseEvent<T> e)
            {
            }
        }

        #endregion

        protected virtual KeyBindingContainer<T> CreateKeyBindingContainer(RulesetInfo ruleset, int variant, SimultaneousBindingMode unique)
            => new RulesetKeyBindingContainer(ruleset, variant, unique);

        public partial class RulesetKeyBindingContainer : DatabasedKeyBindingContainer<T>
        {
            protected override bool HandleRepeats => false;

            public RulesetKeyBindingContainer(RulesetInfo ruleset, int variant, SimultaneousBindingMode unique)
                : base(ruleset, variant, unique)
            {
            }

            protected override void ReloadMappings(IQueryable<RealmKeyBinding> realmKeyBindings)
            {
                base.ReloadMappings(realmKeyBindings);

                KeyBindings = KeyBindings.Where(b => RealmKeyBindingStore.CheckValidForGameplay(b.KeyCombination)).ToList();
                RealmKeyBindingStore.ClearDuplicateBindings(KeyBindings);
            }
        }
    }

    public class RulesetInputManagerInputState<T> : InputState
        where T : struct
    {
        public ReplayState<T> LastReplayState;

        public RulesetInputManagerInputState(InputState state = null)
            : base(state)
        {
        }
    }
}
