﻿using Artemis.Core.Plugins.Abstract;

namespace Artemis.Plugins.LayerBrushes.Noise
{
    public class NoiseBrushProvider : LayerBrushProvider
    {
        public override void EnablePlugin()
        {
            AddLayerBrushDescriptor<NoiseBrush>("Noise", "A brush of that shows an animated random noise", "ScatterPlot");
        }

        public override void DisablePlugin()
        {
        }
    }
}