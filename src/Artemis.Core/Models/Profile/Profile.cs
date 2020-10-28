﻿using System;
using System.Collections.Generic;
using System.Linq;
using Artemis.Core.Modules;
using Artemis.Storage.Entities.Profile;
using SkiaSharp;

namespace Artemis.Core
{
    public sealed class Profile : ProfileElement
    {
        private bool _isActivated;

        internal Profile(ProfileModule module, string name)
        {
            ProfileEntity = new ProfileEntity();
            EntityId = Guid.NewGuid();

            Profile = this;
            Module = module;
            Name = name;
            UndoStack = new Stack<string>();
            RedoStack = new Stack<string>();

            Folder _ = new Folder(this, "Root folder");
            Save();
        }

        internal Profile(ProfileModule module, ProfileEntity profileEntity)
        {
            Profile = this;
            ProfileEntity = profileEntity;
            EntityId = profileEntity.Id;

            Module = module;
            UndoStack = new Stack<string>();
            RedoStack = new Stack<string>();

            Load();
        }

        public ProfileModule Module { get; }

        public bool IsActivated
        {
            get => _isActivated;
            private set => SetAndNotify(ref _isActivated, value);
        }

        internal ProfileEntity ProfileEntity { get; set; }

        internal Stack<string> UndoStack { get; set; }
        internal Stack<string> RedoStack { get; set; }

        public override void Update(double deltaTime)
        {
            lock (this)
            {
                if (Disposed)
                    throw new ObjectDisposedException("Profile");
                if (!IsActivated)
                    throw new ArtemisCoreException($"Cannot update inactive profile: {this}");

                foreach (ProfileElement profileElement in Children)
                    profileElement.Update(deltaTime);
            }
        }

        public override void Render(SKCanvas canvas, SKImageInfo canvasInfo)
        {
            lock (this)
            {
                if (Disposed)
                    throw new ObjectDisposedException("Profile");
                if (!IsActivated)
                    throw new ArtemisCoreException($"Cannot render inactive profile: {this}");

                foreach (ProfileElement profileElement in Children)
                    profileElement.Render(canvas, canvasInfo);
            }
        }

        /// <inheritdoc />
        public override void Reset()
        {
            foreach (ProfileElement child in Children)
                child.Reset();
        }

        public Folder GetRootFolder()
        {
            if (Disposed)
                throw new ObjectDisposedException("Profile");

            return (Folder) Children.Single();
        }

        internal override void Load()
        {
            if (Disposed)
                throw new ObjectDisposedException("Profile");

            Name = ProfileEntity.Name;

            lock (ChildrenList)
            {
                // Remove the old profile tree
                foreach (ProfileElement profileElement in Children)
                    profileElement.Dispose();
                ChildrenList.Clear();

                // Populate the profile starting at the root, the rest is populated recursively
                FolderEntity rootFolder = ProfileEntity.Folders.FirstOrDefault(f => f.ParentId == EntityId);
                if (rootFolder == null)
                {
                    Folder _ = new Folder(this, "Root folder");
                }
                else
                    AddChild(new Folder(this, this, rootFolder));
            }
        }

        public override string ToString()
        {
            return $"[Profile] {nameof(Name)}: {Name}, {nameof(IsActivated)}: {IsActivated}, {nameof(Module)}: {Module}";
        }

        protected override void Dispose(bool disposing)
        {
            if (!disposing)
                return;

            OnDeactivating();

            foreach (ProfileElement profileElement in Children)
                profileElement.Dispose();
            ChildrenList.Clear();

            IsActivated = false;
            Disposed = true;
        }

        internal override void Save()
        {
            if (Disposed)
                throw new ObjectDisposedException("Profile");

            ProfileEntity.Id = EntityId;
            ProfileEntity.PluginGuid = Module.PluginInfo.Guid;
            ProfileEntity.Name = Name;
            ProfileEntity.IsActive = IsActivated;

            foreach (ProfileElement profileElement in Children)
                profileElement.Save();

            ProfileEntity.Folders.Clear();
            ProfileEntity.Folders.AddRange(GetAllFolders().Select(f => f.FolderEntity));

            ProfileEntity.Layers.Clear();
            ProfileEntity.Layers.AddRange(GetAllLayers().Select(f => f.LayerEntity));
        }

        internal void Activate(ArtemisSurface surface)
        {
            lock (this)
            {
                if (Disposed)
                    throw new ObjectDisposedException("Profile");
                if (IsActivated)
                    return;

                PopulateLeds(surface);
                OnActivated();
                IsActivated = true;
            }
        }

        internal void PopulateLeds(ArtemisSurface surface)
        {
            if (Disposed)
                throw new ObjectDisposedException("Profile");

            foreach (Layer layer in GetAllLayers())
                layer.PopulateLeds(surface);
        }

        #region Events

        /// <summary>
        ///     Occurs when the profile has been activated.
        /// </summary>
        public event EventHandler Activated;

        /// <summary>
        ///     Occurs when the profile is being deactivated.
        /// </summary>
        public event EventHandler Deactivated;

        private void OnActivated()
        {
            Activated?.Invoke(this, EventArgs.Empty);
        }

        private void OnDeactivating()
        {
            Deactivated?.Invoke(this, EventArgs.Empty);
        }

        #endregion
    }
}