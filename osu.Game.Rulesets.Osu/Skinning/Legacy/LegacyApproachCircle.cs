// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Diagnostics;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.Skinning.Legacy
{
    public partial class LegacyApproachCircle : Sprite
    {
        [Resolved]
        private DrawableHitObject drawableObject { get; set; } = null!;

        private IBindable<Color4> accentColour = null!;

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin)
        {
            var texture = skin.GetTexture(@"approachcircle");
            Debug.Assert(texture != null);
            Texture = texture.WithMaximumSize(OsuHitObject.OBJECT_DIMENSIONS * 2);

            // account for the sprite being used for the default approach circle being taken from stable,
            // when hitcircles have 5px padding on each size. this should be removed if we update the sprite.
            Scale = new Vector2(128 / 118f);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            accentColour = drawableObject.AccentColour.GetBoundCopy();
            accentColour.BindValueChanged(colour => Colour = LegacyColourCompatibility.DisallowZeroAlpha(colour.NewValue), true);
        }
    }
}
