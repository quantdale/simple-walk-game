using System;
using System.Collections.Generic;

namespace WalkGame.Shell.Presentation
{
    /// <summary>
    /// D-169: pluggable key → asset lookup (a sprite, a prefab, a mesh; whatever the approved visual
    /// direction produces). This seam ships with no art: the only catalogs are empty or test ones.
    /// </summary>
    public interface IPresentationAssetCatalog<TAsset> where TAsset : class
    {
        bool TryGet(string key, out TAsset? asset);
    }

    /// <summary>A catalog backed by a dictionary (tests, or an art pipeline's generated table).</summary>
    public sealed class DictionaryAssetCatalog<TAsset> : IPresentationAssetCatalog<TAsset> where TAsset : class
    {
        private readonly Dictionary<string, TAsset> _assets = new Dictionary<string, TAsset>(StringComparer.Ordinal);
        public DictionaryAssetCatalog<TAsset> Add(string key, TAsset asset) { _assets[key] = asset; return this; }
        public bool TryGet(string key, out TAsset? asset)
        {
            if (key != null && _assets.TryGetValue(key, out var a)) { asset = a; return true; }
            asset = null; return false;
        }
    }

    /// <summary>A catalog with nothing in it: every key falls back. This is the state until a visual direction is approved (D-164).</summary>
    public sealed class EmptyAssetCatalog<TAsset> : IPresentationAssetCatalog<TAsset> where TAsset : class
    {
        public bool TryGet(string key, out TAsset? asset) { asset = null; return false; }
    }

    public enum AssetResolution
    {
        /// <summary>The exact key was in the catalog.</summary>
        Exact = 0,
        /// <summary>The exact key was missing; a less specific key of the same subject was used (e.g. <c>lm.wetland</c> for <c>lm.wetland@thriving</c>).</summary>
        SubjectFallback = 1,
        /// <summary>Only the catalog-wide fallback asset was found.</summary>
        GlobalFallback = 2,
        /// <summary>Nothing at all; the view must draw its own neutral placeholder (or nothing). Never throws.</summary>
        Missing = 3,
    }

    public readonly struct ResolvedAsset<TAsset> where TAsset : class
    {
        public ResolvedAsset(string requestedKey, string? resolvedKey, TAsset? asset, AssetResolution resolution)
        { RequestedKey = requestedKey; ResolvedKey = resolvedKey; Asset = asset; Resolution = resolution; }
        public string RequestedKey { get; }
        public string? ResolvedKey { get; }
        public TAsset? Asset { get; }
        public AssetResolution Resolution { get; }
        public bool IsFallback => Resolution != AssetResolution.Exact;
    }

    /// <summary>
    /// Resolves style-neutral keys with a deterministic fallback chain:
    /// exact key → subject key (text before '@') → <see cref="GlobalFallbackKey"/> → Missing.
    /// Every key that did not resolve exactly is recorded in <see cref="MissingKeys"/> so an art
    /// pipeline can list what is still to be made. A missing asset never throws.
    /// </summary>
    public sealed class PresentationAssetResolver<TAsset> where TAsset : class
    {
        public const string GlobalFallbackKey = "fallback.missing";

        private readonly IPresentationAssetCatalog<TAsset> _catalog;
        private readonly SortedSet<string> _missing = new SortedSet<string>(StringComparer.Ordinal);

        public PresentationAssetResolver(IPresentationAssetCatalog<TAsset> catalog) =>
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));

        /// <summary>Keys requested so far that had no exact asset, in ordinal order.</summary>
        public IReadOnlyCollection<string> MissingKeys => _missing;

        public ResolvedAsset<TAsset> Resolve(string key)
        {
            if (string.IsNullOrEmpty(key)) return new ResolvedAsset<TAsset>(key ?? "", null, null, AssetResolution.Missing);
            if (_catalog.TryGet(key, out var exact) && exact != null) return new ResolvedAsset<TAsset>(key, key, exact, AssetResolution.Exact);
            _missing.Add(key);
            int at = key.IndexOf('@');
            if (at > 0)
            {
                string subject = key.Substring(0, at);
                if (_catalog.TryGet(subject, out var bySubject) && bySubject != null)
                    return new ResolvedAsset<TAsset>(key, subject, bySubject, AssetResolution.SubjectFallback);
            }
            if (_catalog.TryGet(GlobalFallbackKey, out var global) && global != null)
                return new ResolvedAsset<TAsset>(key, GlobalFallbackKey, global, AssetResolution.GlobalFallback);
            return new ResolvedAsset<TAsset>(key, null, null, AssetResolution.Missing);
        }
    }
}
