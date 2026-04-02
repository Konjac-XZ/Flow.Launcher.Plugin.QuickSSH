using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Flow.Launcher.Plugin.QuickSSH
{
    /// <summary>
    /// Persisted plugin data: SSH/SCP profiles, custom shells, and selected shell.
    /// </summary>
    public class UserData
    {
        public string PluginVersion { get; set; } = "2.0";

        // ── Structured profiles (canonical format, v2+) ────────────────────────────

        [JsonProperty]
        private Dictionary<string, SshProfile> ProfilesLists { get; set; } = new();

        [JsonIgnore]
        public AutoSaveDictionary<string, SshProfile> Profiles { get; private set; }

        // ── Legacy raw-string profiles (v1, migration source only) ─────────────────
        // Kept as nullable so that newly created files never write this field.

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        private Dictionary<string, string> EntriesLists { get; set; }

        [JsonProperty]
        private Dictionary<string, string> CustomShellLists { get; set; } = new();

        [JsonIgnore]
        public AutoSaveDictionary<string, string> CustomShell { get; private set; }

        public string? SelectedCustomShell { get; set; }

        /// <summary>
        /// Binds auto-save callbacks after construction or deserialization.
        /// Migrates any legacy raw-string profiles to structured <see cref="SshProfile"/> objects.
        /// </summary>
        /// <param name="onChanged">Callback invoked on every profile or shell mutation.</param>
        /// <returns>
        /// <see langword="true"/> when v1 legacy data was found and migrated;
        /// the caller should persist immediately so the disk file reflects the new v2 format.
        /// </returns>
        public bool Attach(Action onChanged)
        {
            ProfilesLists ??= new Dictionary<string, SshProfile>();
            CustomShellLists ??= new Dictionary<string, string>();

            bool migrated = false;

            // One-time migration from v1 raw-string storage (EntriesLists) to the canonical
            // structured model (ProfilesLists).  We only migrate entries that are not already
            // present in ProfilesLists so a mixed v1/v2 file is handled safely.
            // Fields that cannot be parsed from the raw command string are preserved verbatim
            // in SshProfile.ExtraArgs so no data is ever silently lost.
            if (EntriesLists != null && EntriesLists.Count > 0)
            {
                foreach (var kvp in EntriesLists)
                {
                    if (!ProfilesLists.ContainsKey(kvp.Key))
                        ProfilesLists[kvp.Key] = SshProfile.ParseFromLegacyCommand(kvp.Value);
                }

                // Null out the legacy field so it is absent from the next serialization.
                EntriesLists = null;
                migrated = true;
            }

            Profiles = new AutoSaveDictionary<string, SshProfile>(ProfilesLists, onChanged);
            CustomShell = new AutoSaveDictionary<string, string>(CustomShellLists, onChanged);

            return migrated;
        }
    }

    /// <summary>
    /// Dictionary wrapper that invokes a callback on every mutation.
    /// </summary>
    public sealed class AutoSaveDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    {
        private static readonly Action Noop = () => { };
        private readonly IDictionary<TKey, TValue> _inner;
        private Action _onChanged;

        public AutoSaveDictionary(IDictionary<TKey, TValue> inner, Action onChanged)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _onChanged = onChanged ?? Noop;
        }

        public void SetCallback(Action onChanged) => _onChanged = onChanged ?? Noop;

        public TValue this[TKey key]
        {
            get => _inner[key];
            set { _inner[key] = value; _onChanged(); }
        }

        public ICollection<TKey> Keys => _inner.Keys;
        public ICollection<TValue> Values => _inner.Values;
        public int Count => _inner.Count;
        public bool IsReadOnly => _inner.IsReadOnly;

        public void Add(TKey key, TValue value) { _inner.Add(key, value); _onChanged(); }
        public void Add(KeyValuePair<TKey, TValue> item) { _inner.Add(item); _onChanged(); }
        public bool Remove(TKey key) { var r = _inner.Remove(key); if (r) _onChanged(); return r; }
        public bool Remove(KeyValuePair<TKey, TValue> item) { var r = _inner.Remove(item); if (r) _onChanged(); return r; }
        public void Clear() { _inner.Clear(); _onChanged(); }
        public bool ContainsKey(TKey key) => _inner.ContainsKey(key);
        public bool Contains(KeyValuePair<TKey, TValue> item) => _inner.Contains(item);
        public bool TryGetValue(TKey key, out TValue value) => _inner.TryGetValue(key, out value);
        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) => _inner.CopyTo(array, arrayIndex);
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _inner.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_inner).GetEnumerator();
    }

    /// <summary>
    /// Manages loading and saving of user data to a JSON file.
    /// </summary>
    public class ProfileManager
    {
        private readonly string _path;
        public UserData UserData { get; private set; }

        public ProfileManager(string path)
        {
            _path = path;

            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            if (!File.Exists(_path))
            {
                UserData = new UserData();
                UserData.Attach(SaveConfiguration);
                SaveConfiguration();
            }
            else
            {
                LoadConfiguration();
            }
        }

        public void SaveConfiguration()
        {
            var json = JsonConvert.SerializeObject(UserData, Formatting.Indented);
            var tmp = _path + ".tmp";
            try
            {
                File.WriteAllText(tmp, json);
                File.Move(tmp, _path, overwrite: true);
            }
            finally
            {
                if (File.Exists(tmp))
                    try { File.Delete(tmp); } catch { /* best effort cleanup */ }
            }
        }

        public void LoadConfiguration()
        {
            var json = File.ReadAllText(_path);
            UserData = JsonConvert.DeserializeObject<UserData>(json) ?? new UserData();

            // Attach returns true when v1 raw-string data was migrated.  Persist immediately so
            // the on-disk file switches to the canonical v2 format after the first load.
            bool migrated = UserData.Attach(SaveConfiguration);
            if (migrated)
                SaveConfiguration();
        }
    }
}