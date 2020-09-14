﻿using System;
using System.Collections;
using System.Linq;
using System.Linq.Expressions;
using Artemis.Core.DataModelExpansions;
using Artemis.Storage.Entities.Profile.Abstract;
using Artemis.Storage.Entities.Profile.Conditions;

namespace Artemis.Core
{
    public class DisplayConditionList : DisplayConditionPart
    {
        private bool _disposed;

        public DisplayConditionList(DisplayConditionPart parent)
        {
            Parent = parent;
            Entity = new DisplayConditionListEntity();

            Initialize();
        }

        public DisplayConditionList(DisplayConditionPart parent, DisplayConditionListEntity entity)
        {
            Parent = parent;
            Entity = entity;
            ListOperator = (ListOperator) entity.ListOperator;

            Initialize();
        }

        public DisplayConditionListEntity Entity { get; set; }

        public ListOperator ListOperator { get; set; }
        public DataModel ListDataModel { get; private set; }
        public string ListPropertyPath { get; private set; }

        public Func<object, IList> CompiledListAccessor { get; set; }

        public override bool Evaluate()
        {
            if (_disposed)
                throw new ObjectDisposedException("DisplayConditionList");

            if (CompiledListAccessor == null)
                return false;

            return EvaluateObject(CompiledListAccessor(ListDataModel));
        }

        public override bool EvaluateObject(object target)
        {
            if (_disposed)
                throw new ObjectDisposedException("DisplayConditionList");

            if (!Children.Any())
                return false;
            if (!(target is IList list))
                return false;

            var objectList = list.Cast<object>();
            return ListOperator switch
            {
                ListOperator.Any => objectList.Any(o => Children[0].EvaluateObject(o)),
                ListOperator.All => objectList.All(o => Children[0].EvaluateObject(o)),
                ListOperator.None => objectList.Any(o => !Children[0].EvaluateObject(o)),
                ListOperator.Count => false,
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        public void UpdateList(DataModel dataModel, string path)
        {
            if (_disposed)
                throw new ObjectDisposedException("DisplayConditionList");

            if (dataModel != null && path == null)
                throw new ArtemisCoreException("If a data model is provided, a path is also required");
            if (dataModel == null && path != null)
                throw new ArtemisCoreException("If path is provided, a data model is also required");

            if (dataModel != null)
            {
                if (!dataModel.ContainsPath(path))
                    throw new ArtemisCoreException($"Data model of type {dataModel.GetType().Name} does not contain a property at path '{path}'");
                if (dataModel.GetListTypeAtPath(path) == null)
                    throw new ArtemisCoreException($"The path '{path}' does not contain a list");
            }

            // Remove the old root group that was tied to the old data model
            while (Children.Any())
                RemoveChild(Children[0]);

            ListDataModel = dataModel;
            ListPropertyPath = path;

            if (dataModel == null)
                return;

            // Create a new root group
            AddChild(new DisplayConditionGroup(this));
            CreateExpression();
        }

        public void CreateExpression()
        {
            if (_disposed)
                throw new ObjectDisposedException("DisplayConditionList");

            var parameter = Expression.Parameter(typeof(object), "listDataModel");
            var accessor = ListPropertyPath.Split('.').Aggregate<string, Expression>(
                Expression.Convert(parameter, ListDataModel.GetType()),
                (expression, s) => Expression.Convert(Expression.Property(expression, s), typeof(IList)));

            var lambda = Expression.Lambda<Func<object, IList>>(accessor, parameter);
            CompiledListAccessor = lambda.Compile();
        }
        
        internal override void Save()
        {
            // Target list
            if (ListDataModel != null)
            {
                Entity.ListDataModelGuid = ListDataModel.PluginInfo.Guid;
                Entity.ListPropertyPath = ListPropertyPath;
            }

            // Operator
            Entity.ListOperator = (int) ListOperator;

            // Children
            Entity.Children.Clear();
            Entity.Children.AddRange(Children.Select(c => c.GetEntity()));
            foreach (var child in Children)
                child.Save();
        }

        internal override DisplayConditionPartEntity GetEntity()
        {
            return Entity;
        }

        internal void Initialize()
        {
            DataModelStore.DataModelAdded += DataModelStoreOnDataModelAdded;
            DataModelStore.DataModelRemoved += DataModelStoreOnDataModelRemoved;
            if (Entity.ListDataModelGuid == null)
                return;

            // Get the data model and ensure the path is valid
            var dataModel = DataModelStore.Get(Entity.ListDataModelGuid.Value)?.DataModel;
            if (dataModel == null || !dataModel.ContainsPath(Entity.ListPropertyPath))
                return;

            // Populate properties and create the accessor expression
            ListDataModel = dataModel;
            ListPropertyPath = Entity.ListPropertyPath;
            CreateExpression();

            // There should only be one child and it should be a group
            if (Entity.Children.SingleOrDefault() is DisplayConditionGroupEntity rootGroup)
                AddChild(new DisplayConditionGroup(this, rootGroup));
            else
            {
                Entity.Children.Clear();
                AddChild(new DisplayConditionGroup(this));
            }
        }


        #region Event handlers

        private void DataModelStoreOnDataModelAdded(object sender, DataModelStoreEvent e)
        {
            var dataModel = e.Registration.DataModel;
            if (dataModel.PluginInfo.Guid == Entity.ListDataModelGuid && dataModel.ContainsPath(Entity.ListPropertyPath))
            {
                ListDataModel = dataModel;
                ListPropertyPath = Entity.ListPropertyPath;
                CreateExpression();
            }
        }

        private void DataModelStoreOnDataModelRemoved(object sender, DataModelStoreEvent e)
        {
            if (ListDataModel != e.Registration.DataModel)
                return;

            ListDataModel = null;
            CompiledListAccessor = null;
        }

        #endregion

        #region IDisposable

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            _disposed = true;

            DataModelStore.DataModelAdded -= DataModelStoreOnDataModelAdded;
            DataModelStore.DataModelRemoved -= DataModelStoreOnDataModelRemoved;

            foreach (var child in Children)
                child.Dispose();

            base.Dispose(disposing);
        }

        #endregion
    }

    public enum ListOperator
    {
        Any,
        All,
        None,
        Count
    }
}