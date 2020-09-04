﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Artemis.Core.DataModelExpansions;
using Artemis.Core.Services;
using Artemis.Storage.Entities.Profile.DataBindings;

namespace Artemis.Core
{
    /// <summary>
    ///     A data binding that binds a certain <see cref="BaseLayerProperty" /> to a value inside a <see cref="DataModel" />
    /// </summary>
    public class DataBinding
    {
        private readonly List<DataBindingModifier> _modifiers = new List<DataBindingModifier>();
        private bool _isInitialized;

        internal DataBinding(BaseLayerProperty layerProperty, PropertyInfo targetProperty)
        {
            LayerProperty = layerProperty;
            TargetProperty = targetProperty;
            Entity = new DataBindingEntity();

            ApplyToEntity();
        }

        internal DataBinding(BaseLayerProperty layerProperty, DataBindingEntity entity)
        {
            LayerProperty = layerProperty;
            Entity = entity;

            ApplyToDataBinding();
        }

        /// <summary>
        ///     Gets the layer property this data binding targets
        /// </summary>
        public BaseLayerProperty LayerProperty { get; }

        /// <summary>
        ///     Gets the inner property this data binding targets
        /// </summary>
        public PropertyInfo TargetProperty { get; private set; }

        /// <summary>
        ///     Gets the currently used instance of the data model that contains the source of the data binding
        /// </summary>
        public DataModel SourceDataModel { get; private set; }

        /// <summary>
        ///     Gets the path of the source property in the <see cref="SourceDataModel" />
        /// </summary>
        public string SourcePropertyPath { get; private set; }

        public DataBindingMode Mode { get; set; }

        /// <summary>
        ///     Gets or sets the easing time of the data binding
        /// </summary>
        public TimeSpan EasingTime { get; set; }

        /// <summary>
        ///     Gets ors ets the easing function of the data binding
        /// </summary>
        public Easings.Functions EasingFunction { get; set; }

        /// <summary>
        ///     Gets a list of modifiers applied to this data binding
        /// </summary>
        public IReadOnlyList<DataBindingModifier> Modifiers => _modifiers.AsReadOnly();

        /// <summary>
        ///     Gets the compiled function that gets the current value of the data binding target
        /// </summary>
        public Func<DataModel, object> CompiledTargetAccessor { get; private set; }

        internal DataBindingEntity Entity { get; }

        /// <summary>
        ///     Adds a modifier to the data binding's <see cref="Modifiers" /> collection
        /// </summary>
        public void AddModifier(DataBindingModifier modifier)
        {
            if (!_modifiers.Contains(modifier))
            {
                modifier.DataBinding = this;
                _modifiers.Add(modifier);
            }
        }

        /// <summary>
        ///     Removes a modifier from the data binding's <see cref="Modifiers" /> collection
        /// </summary>
        public void RemoveModifier(DataBindingModifier modifier)
        {
            if (_modifiers.Contains(modifier))
            {
                modifier.DataBinding = null;
                _modifiers.Remove(modifier);
            }
        }

        /// <summary>
        ///     Updates the source of the data binding and re-compiles the expression
        /// </summary>
        /// <param name="dataModel">The data model of the source</param>
        /// <param name="path">The path pointing to the source inside the data model</param>
        public void UpdateSource(DataModel dataModel, string path)
        {
            if (dataModel != null && path == null)
                throw new ArtemisCoreException("If a data model is provided, a path is also required");
            if (dataModel == null && path != null)
                throw new ArtemisCoreException("If path is provided, a data model is also required");

            if (dataModel != null)
            {
                if (!dataModel.ContainsPath(path))
                    throw new ArtemisCoreException($"Data model of type {dataModel.GetType().Name} does not contain a property at path '{path}'");
            }

            SourceDataModel = dataModel;
            SourcePropertyPath = path;
            CreateExpression();
        }

        /// <summary>
        ///     Gets the current value of the data binding
        /// </summary>
        /// <param name="baseValue">The base value of the property the data binding is applied to</param>
        /// <returns></returns>
        public object GetValue(object baseValue)
        {
            // Validating this is kinda expensive, it'll fail on ChangeType later anyway ^^
            // var targetType = TargetProperty.PropertyType;
            // if (!targetType.IsCastableFrom(baseValue.GetType()))
            // {
            //     throw new ArtemisCoreException($"The provided current value type ({baseValue.GetType().Name}) not match the " +
            //                                    $"target property type ({targetType.Name})");
            // }

            if (CompiledTargetAccessor == null)
                return baseValue;

            var dataBindingValue = CompiledTargetAccessor(SourceDataModel);
            foreach (var dataBindingModifier in Modifiers)
                dataBindingValue = dataBindingModifier.Apply(dataBindingValue);

            return Convert.ChangeType(dataBindingValue, TargetProperty.PropertyType);
        }

        internal void Update(double deltaTime)
        {
        }

        internal void ApplyToEntity()
        {
            // General 
            Entity.TargetProperty = TargetProperty?.Name;
            Entity.DataBindingMode = (int) Mode;
            Entity.EasingTime = EasingTime;
            Entity.EasingFunction = (int) EasingFunction;

            // Data model
            Entity.SourceDataModelGuid = SourceDataModel?.PluginInfo?.Guid;
            Entity.SourcePropertyPath = SourcePropertyPath;

            // Modifiers
            Entity.Modifiers.Clear();
            foreach (var dataBindingModifier in Modifiers)
            {
                dataBindingModifier.ApplyToEntity();
                Entity.Modifiers.Add(dataBindingModifier.Entity);
            }
        }

        internal void ApplyToDataBinding()
        {
            // General
            TargetProperty = LayerProperty.GetDataBindingProperties()?.FirstOrDefault(p => p.Name == Entity.TargetProperty);
            Mode = (DataBindingMode) Entity.DataBindingMode;
            EasingTime = Entity.EasingTime;
            EasingFunction = (Easings.Functions) Entity.EasingFunction;

            // Data model is done during Initialize

            // Modifiers
            foreach (var dataBindingModifierEntity in Entity.Modifiers)
                _modifiers.Add(new DataBindingModifier(this, dataBindingModifierEntity));
        }

        internal void Initialize(IDataModelService dataModelService, IDataBindingService dataBindingService)
        {
            if (_isInitialized)
                throw new ArtemisCoreException("Data binding is already initialized");

            // Source
            if (Entity.SourceDataModelGuid != null)
            {
                var dataModel = dataModelService.GetPluginDataModelByGuid(Entity.SourceDataModelGuid.Value);
                if (dataModel != null && dataModel.ContainsPath(Entity.SourcePropertyPath))
                    UpdateSource(dataModel, Entity.SourcePropertyPath);
            }

            // Modifiers
            foreach (var dataBindingModifier in Modifiers)
                dataBindingModifier.Initialize(dataModelService, dataBindingService);

            _isInitialized = true;
        }

        private void CreateExpression()
        {
            var listType = SourceDataModel.GetListTypeInPath(SourcePropertyPath);
            if (listType != null)
                throw new ArtemisCoreException($"Cannot create a regular accessor at path {SourcePropertyPath} because the path contains a list");

            var parameter = Expression.Parameter(typeof(DataModel), "targetDataModel");
            var accessor = SourcePropertyPath.Split('.').Aggregate<string, Expression>(
                Expression.Convert(parameter, SourceDataModel.GetType()), // Cast to the appropriate type
                Expression.Property
            );

            var returnValue = Expression.Convert(accessor, typeof(object));

            var lambda = Expression.Lambda<Func<DataModel, object>>(returnValue, parameter);
            CompiledTargetAccessor = lambda.Compile();
        }
    }

    /// <summary>
    ///     A mode that determines how the data binding is applied to the layer property
    /// </summary>
    public enum DataBindingMode
    {
        /// <summary>
        ///     Replaces the layer property value with the data binding value
        /// </summary>
        Replace,

        /// <summary>
        ///     Adds the data binding value to the layer property value
        /// </summary>
        Add
    }
}