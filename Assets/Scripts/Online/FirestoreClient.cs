using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

namespace Online {
    // Minimal Cloud Firestore client over the REST API.
    // https://firebase.google.com/docs/firestore/use-rest-api
    public class FirestoreClient {
        private readonly string _baseUrl;
        private readonly string _docRoot;   // documents' resource name prefix, for :commit
        private readonly FirebaseAuthService _auth;

        // host: FirebaseConfig.firestoreUrl, a local emulator (docs/firebase/SETUP.md); empty = the real one
        public FirestoreClient(string projectId, FirebaseAuthService auth, string host = null) {
            _docRoot = "projects/" + projectId + "/databases/(default)/documents";
            _baseUrl = (string.IsNullOrEmpty(host) ? "https://firestore.googleapis.com" : host.TrimEnd('/')) + "/v1/" + _docRoot;
            _auth = auth;
        }

        // "projects/<id>/databases/(default)/documents", what a query's referenceValue starts with
        public string DocRoot => _docRoot;

        // done(ok, json). A missing document counts as ok with a null body.
        public IEnumerator Get(string path, Action<bool, string> done) {
            yield return Send("GET", "/" + path, null, (code, body) => {
                if (code == 404) {
                    done?.Invoke(true, null);
                } else {
                    done?.Invoke(code == 200, body);
                }
            });
        }

        // Creates or updates only the given fields of a document.
        public IEnumerator Patch(string path, FirestoreFields fields, Action<bool> done) {
            if (fields.HasServerTimes) {
                // PATCH can't ask for the server's clock; a commit with field transforms can
                yield return Send("POST", ":commit", fields.ToCommitJson(_docRoot + "/" + path),
                    (code, _) => done?.Invoke(code == 200));
                yield break;
            }
            StringBuilder query = new StringBuilder();
            foreach (string name in fields.Names) {
                query.Append(query.Length == 0 ? "?" : "&");
                query.Append("updateMask.fieldPaths=").Append(UnityWebRequest.EscapeURL(name));
            }
            yield return Send("PATCH", "/" + path + query, fields.ToJson(), (code, _) => done?.Invoke(code == 200));
        }

        // Creates a document only if it isn't there yet (a nickname, a friend code, a friend request).
        // done(code): 200 when it was created; anything else was refused or could not be sent.
        public IEnumerator Create(string path, FirestoreFields fields, Action<long> done) {
            yield return Send("POST", ":commit", fields.ToCommitJson(_docRoot + "/" + path, true), (code, _) => done?.Invoke(code));
        }

        // Several writes in one commit, all or nothing: each one from Write() or DeleteWrite().
        // done(code): 200 when every write went in.
        public IEnumerator Commit(IList<string> writes, Action<long> done) {
            yield return Send("POST", ":commit", "{\"writes\":[" + string.Join(",", writes) + "]}", (code, _) => done?.Invoke(code));
        }

        // one write for Commit: updates these fields of the document (mustBeNew: only if it isn't there)
        public string Write(string path, FirestoreFields fields, bool mustBeNew = false) {
            return fields.ToWriteJson(_docRoot + "/" + path, mustBeNew);
        }

        // one write for Commit: deletes the document
        public string DeleteWrite(string path) {
            return "{\"delete\":\"" + Json.Escape(_docRoot + "/" + path) + "\"}";
        }

        public IEnumerator Delete(string path, Action<bool> done) {
            yield return Send("DELETE", "/" + path, null, (code, _) => done?.Invoke(code == 200));
        }

        // Runs a structured query. The response is a JSON array, one entry per document.
        public IEnumerator RunQuery(string structuredQueryJson, Action<bool, string> done) {
            string body = "{\"structuredQuery\":" + structuredQueryJson + "}";
            yield return Send("POST", ":runQuery", body, (code, res) => done?.Invoke(code == 200, res));
        }

        private IEnumerator Send(string method, string path, string body, Action<long, string> done) {
            string token = null;
            yield return _auth.GetIdToken(t => token = t);

            using (UnityWebRequest req = new UnityWebRequest(_baseUrl + path, method)) {
                if (body != null) {
                    req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                    req.SetRequestHeader("Content-Type", "application/json");
                }
                req.downloadHandler = new DownloadHandlerBuffer();
                if (token != null) {
                    req.SetRequestHeader("Authorization", "Bearer " + token);
                }
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success && req.responseCode != 404) {
                    Debug.LogWarning("[Online] Firestore " + method + " " + path + " failed (" + req.responseCode + "): " +
                                     req.downloadHandler.text);
                }
                done?.Invoke(req.responseCode, req.downloadHandler.text);
            }
        }
    }

    // Builds the "fields" object of a Firestore document in its typed-value JSON format.
    public class FirestoreFields {
        private readonly Dictionary<string, string> _values = new Dictionary<string, string>();
        private readonly List<string> _serverTimes = new List<string>();

        public IEnumerable<string> Names => _values.Keys;
        public bool HasServerTimes => _serverTimes.Count > 0;

        public FirestoreFields Set(string name, string value) {
            _values[name] = "{\"stringValue\":\"" + Json.Escape(value) + "\"}";
            return this;
        }

        public FirestoreFields Set(string name, long value) {
            _values[name] = "{\"integerValue\":\"" + value.ToString(CultureInfo.InvariantCulture) + "\"}";
            return this;
        }

        public FirestoreFields Set(string name, bool value) {
            _values[name] = "{\"booleanValue\":" + (value ? "true" : "false") + "}";
            return this;
        }

        public FirestoreFields Set(string name, IEnumerable<string> list) {
            StringBuilder sb = new StringBuilder("{\"arrayValue\":{\"values\":[");
            bool first = true;
            foreach (string value in list) {
                if (!first) sb.Append(',');
                sb.Append("{\"stringValue\":\"").Append(Json.Escape(value)).Append("\"}");
                first = false;
            }
            _values[name] = sb.Append("]}}").ToString();
            return this;
        }

        public FirestoreFields SetTimestamp(string name, DateTime utc) {
            string iso = utc.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
            _values[name] = "{\"timestampValue\":\"" + iso + "\"}";
            return this;
        }

        // The field gets the time the server receives the write (firestore.rules can check it
        // against request.time, so the client's clock can't be faked).
        public FirestoreFields SetServerTime(string name) {
            if (!_serverTimes.Contains(name)) _serverTimes.Add(name);
            return this;
        }

        // A :commit body with one write that updates only these fields of the document.
        // mustBeNew: it fails if the document already exists.
        public string ToCommitJson(string documentName, bool mustBeNew = false) {
            return "{\"writes\":[" + ToWriteJson(documentName, mustBeNew) + "]}";
        }

        // the same, as one write of a :commit (FirestoreClient.Commit)
        public string ToWriteJson(string documentName, bool mustBeNew = false) {
            StringBuilder sb = new StringBuilder("{\"update\":{\"name\":\"");
            sb.Append(Json.Escape(documentName)).Append("\",").Append(ToJson().Substring(1)).Append(',');
            sb.Append("\"updateMask\":{\"fieldPaths\":[");
            bool first = true;
            foreach (string name in _values.Keys) {
                if (!first) sb.Append(',');
                sb.Append('"').Append(Json.Escape(name)).Append('"');
                first = false;
            }
            sb.Append("]},\"updateTransforms\":[");
            for (int i = 0; i < _serverTimes.Count; i++) {
                if (i > 0) sb.Append(',');
                sb.Append("{\"fieldPath\":\"").Append(Json.Escape(_serverTimes[i])).Append("\",\"setToServerValue\":\"REQUEST_TIME\"}");
            }
            sb.Append(']');
            if (mustBeNew) sb.Append(",\"currentDocument\":{\"exists\":false}");
            return sb.Append('}').ToString();
        }

        public string ToJson() {
            StringBuilder sb = new StringBuilder("{\"fields\":{");
            bool first = true;
            foreach (KeyValuePair<string, string> pair in _values) {
                if (!first) sb.Append(',');
                sb.Append('"').Append(Json.Escape(pair.Key)).Append("\":").Append(pair.Value);
                first = false;
            }
            return sb.Append("}}").ToString();
        }
    }

    // Typed values as Firestore returns them, for JsonUtility.
    [Serializable] public class FsString { public string stringValue; }
    [Serializable] public class FsInteger {
        public string integerValue;
        public long Value => long.TryParse(integerValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out long v) ? v : 0;
    }
    [Serializable] public class FsBool { public bool booleanValue; }

    // A document as Firestore returns it, read by field type: JsonUtility needs a class per
    // document and these (stats, perfis) have dozens of fields, some of them optional.
    public class FsDoc {
        private static readonly Regex IntField = new Regex(@"""(\w+)""\s*:\s*\{\s*""integerValue""\s*:\s*""(-?\d+)""");
        private static readonly Regex TextField = new Regex(@"""(\w+)""\s*:\s*\{\s*""stringValue""\s*:\s*""((?:[^""\\]|\\.)*)""");
        private static readonly Regex TimeField = new Regex(@"""(\w+)""\s*:\s*\{\s*""timestampValue""\s*:\s*""([^""]+)""");
        private static readonly Regex ListField = new Regex(@"""(\w+)""\s*:\s*\{\s*""arrayValue""\s*:\s*\{(.*?)\}\s*\}", RegexOptions.Singleline);
        private static readonly Regex ListItem = new Regex(@"""stringValue""\s*:\s*""((?:[^""\\]|\\.)*)""");
        private static readonly Regex DocName = new Regex(@"""name""\s*:\s*""([^""]+/documents/[^""]+)""");

        public string Id { get; private set; }
        public readonly Dictionary<string, long> Ints = new Dictionary<string, long>();
        public readonly Dictionary<string, string> Texts = new Dictionary<string, string>();
        public readonly Dictionary<string, DateTime> Times = new Dictionary<string, DateTime>();
        public readonly Dictionary<string, List<string>> Lists = new Dictionary<string, List<string>>();

        public long Int(string name) => Ints.TryGetValue(name, out long value) ? value : 0;
        public string Text(string name) => Texts.TryGetValue(name, out string value) ? value : "";

        public static FsDoc Parse(string json) {
            if (string.IsNullOrEmpty(json)) return null;
            FsDoc doc = new FsDoc();
            Match name = DocName.Match(json);
            if (name.Success) doc.Id = name.Groups[1].Value.Substring(name.Groups[1].Value.LastIndexOf('/') + 1);
            foreach (Match m in ListField.Matches(json)) {
                List<string> items = new List<string>();
                foreach (Match item in ListItem.Matches(m.Groups[2].Value)) items.Add(Unescape(item.Groups[1].Value));
                doc.Lists[m.Groups[1].Value] = items;
            }
            foreach (Match m in IntField.Matches(json)) {
                if (long.TryParse(m.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long value)) doc.Ints[m.Groups[1].Value] = value;
            }
            foreach (Match m in TextField.Matches(json)) doc.Texts[m.Groups[1].Value] = Unescape(m.Groups[2].Value);
            foreach (Match m in TimeField.Matches(json)) {
                if (DateTime.TryParse(m.Groups[2].Value, CultureInfo.InvariantCulture,
                        DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime time)) doc.Times[m.Groups[1].Value] = time;
            }
            return doc;
        }

        // the answer of a :runQuery, one FsDoc per document found
        public static List<FsDoc> ParseQuery(string json) {
            List<FsDoc> docs = new List<FsDoc>();
            if (string.IsNullOrEmpty(json)) return docs;
            string[] parts = json.Split(new[] { "\"document\"" }, StringSplitOptions.None);
            for (int i = 1; i < parts.Length; i++) {
                FsDoc doc = Parse(parts[i]);
                if (doc != null && !string.IsNullOrEmpty(doc.Id)) docs.Add(doc);
            }
            return docs;
        }

        private static string Unescape(string text) {
            if (text.IndexOf('\\') < 0) return text;
            try {
                return Regex.Unescape(text);
            } catch (ArgumentException) {
                return text;
            }
        }
    }

    public static class Json {
        public static string Escape(string s) {
            if (string.IsNullOrEmpty(s)) return "";
            StringBuilder sb = new StringBuilder(s.Length + 8);
            foreach (char c in s) {
                switch (c) {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) {
                            sb.Append("\\u").Append(((int)c).ToString("x4"));
                        } else {
                            sb.Append(c);
                        }
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
