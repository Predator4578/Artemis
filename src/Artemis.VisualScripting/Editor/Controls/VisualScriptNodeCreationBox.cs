﻿using System;
using System.Collections;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Artemis.Core;
using Artemis.VisualScripting.Editor.Controls.Wrapper;

namespace Artemis.VisualScripting.Editor.Controls
{
    [TemplatePart(Name = PART_SEARCHBOX, Type = typeof(TextBox))]
    [TemplatePart(Name = PART_CONTENT, Type = typeof(ListBox))]
    public class VisualScriptNodeCreationBox : Control
    {
        #region Constants

        private const string PART_SEARCHBOX = "PART_SearchBox";
        private const string PART_CONTENT = "PART_Content";

        #endregion

        #region Properties & Fields

        private TextBox _searchBox;
        private ListBox _contentList;

        private CollectionViewSource _collectionViewSource;
        private ICollectionView _contentView;

        #endregion

        #region DependencyProperties

        public static readonly DependencyProperty AvailableNodesProperty = DependencyProperty.Register(
            "AvailableNodes", typeof(IEnumerable), typeof(VisualScriptNodeCreationBox), new PropertyMetadata(default(IEnumerable), OnItemsSourceChanged));

        public IEnumerable AvailableNodes
        {
            get => (IEnumerable) GetValue(AvailableNodesProperty);
            set => SetValue(AvailableNodesProperty, value);
        }

        public static readonly DependencyProperty SourcePinProperty = DependencyProperty.Register(
            "SourcePin", typeof(VisualScriptPin), typeof(VisualScriptNodeCreationBox), new PropertyMetadata(default(VisualScriptPin), OnSourcePinChanged));

        public VisualScriptPin SourcePin
        {
            get => (VisualScriptPin) GetValue(SourcePinProperty);
            set => SetValue(SourcePinProperty, value);
        }


        public static readonly DependencyProperty CreateNodeCommandProperty = DependencyProperty.Register(
            "CreateNodeCommand", typeof(ICommand), typeof(VisualScriptNodeCreationBox), new PropertyMetadata(default(ICommand)));

        public ICommand CreateNodeCommand
        {
            get => (ICommand) GetValue(CreateNodeCommandProperty);
            set => SetValue(CreateNodeCommandProperty, value);
        }

        #endregion

        #region Methods

        public override void OnApplyTemplate()
        {
            _searchBox = GetTemplateChild(PART_SEARCHBOX) as TextBox ?? throw new NullReferenceException($"The Element '{PART_SEARCHBOX}' is missing.");
            _contentList = GetTemplateChild(PART_CONTENT) as ListBox ?? throw new NullReferenceException($"The Element '{PART_CONTENT}' is missing.");

            _searchBox.TextChanged += OnSearchBoxTextChanged;
            _contentList.IsSynchronizedWithCurrentItem = false;
            _contentList.SelectionChanged += OnContentListSelectionChanged;
            _contentList.SelectionMode = SelectionMode.Single;
            IsVisibleChanged += OnIsVisibleChanged;

            _searchBox.Focus();
            _contentView?.Refresh();
            ItemsSourceChanged();
        }

        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is not true)
                return;

            _searchBox.Focus();
            _searchBox.SelectionStart = 0;
            _searchBox.SelectionLength = _searchBox.Text.Length;
        }

        private void OnSearchBoxTextChanged(object sender, TextChangedEventArgs args)
        {
            _contentView?.Refresh();
        }

        private void OnContentListSelectionChanged(object sender, SelectionChangedEventArgs args)
        {
            if ((args == null) || (_contentList?.SelectedItem == null)) return;

            CreateNodeCommand?.Execute(_contentList.SelectedItem);
        }

        private bool Filter(object o)
        {
            if (_searchBox == null) return false;
            if (o is not NodeData nodeData) return false;

            bool nameContains = nodeData.Name.Contains(_searchBox.Text, StringComparison.OrdinalIgnoreCase);

            if (SourcePin == null || SourcePin.Pin.Type == typeof(object))
                return nameContains;

            if (SourcePin.Pin.Direction == PinDirection.Input)
                return nodeData.OutputType != null && nameContains && (nodeData.OutputType == typeof(object) || nodeData.OutputType.IsAssignableTo(SourcePin.Pin.Type));
            return nodeData.InputType != null && nameContains && (nodeData.InputType == typeof(object) || nodeData.InputType.IsAssignableFrom(SourcePin.Pin.Type));
        }

        private void ItemsSourceChanged()
        {
            if (_contentList == null) return;

            if (AvailableNodes == null)
            {
                _contentView = null;
                _collectionViewSource = null;
            }
            else
            {
                _collectionViewSource = new CollectionViewSource
                {
                    Source = AvailableNodes,
                    SortDescriptions =
                    {
                        new SortDescription(nameof(NodeData.Category), ListSortDirection.Ascending),
                        new SortDescription(nameof(NodeData.Name), ListSortDirection.Ascending)
                    },
                    GroupDescriptions = {new PropertyGroupDescription(nameof(NodeData.Category))}
                };
                _contentView = _collectionViewSource.View;
                _contentView.Filter += Filter;
            }

            _contentList.ItemsSource = _contentView;
        }

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs args) => (d as VisualScriptNodeCreationBox)?.ItemsSourceChanged();

        private static void OnSourcePinChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => (d as VisualScriptNodeCreationBox)?._contentView.Refresh();

        #endregion
    }
}