﻿using Artemis.Core;

namespace Artemis.UI.Shared.Services.ProfileEditor.Commands;

/// <summary>
/// Represents a profile editor command that can be used to enable or disable keyframes on a layer property.
/// </summary>
/// <typeparam name="T"></typeparam>
public class ToggleLayerPropertyKeyframes<T> : IProfileEditorCommand
{
    private readonly bool _enable;
    private readonly LayerProperty<T> _layerProperty;

    /// <summary>
    /// Creates a new instance of the <see cref="ToggleLayerPropertyKeyframes{T}"/> class.
    /// </summary>
    public ToggleLayerPropertyKeyframes(LayerProperty<T> layerProperty, bool enable)
    {
        _layerProperty = layerProperty;
        _enable = enable;
    }

    #region Implementation of IProfileEditorCommand

    /// <inheritdoc />
    public string DisplayName => _enable ? "Enable keyframes" : "Disable keyframes";

    /// <inheritdoc />
    public void Execute()
    {
        _layerProperty.KeyframesEnabled = _enable;
    }

    /// <inheritdoc />
    public void Undo()
    {
        _layerProperty.KeyframesEnabled = !_enable;
    }

    #endregion
}