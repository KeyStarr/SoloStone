using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.Loader
{
	// The per-meta card file (facts/README.md). Parsed once, in the Cards static constructor.
	// A closed-schema reader: Unity's Mono lacks a usable JSON serializer, and a general parser costs more than the XML did.
	internal static class CardFacts
	{
		internal const string Meta = "violet-hold-2026-09";
		private const string Resource = "SabberStoneCore.Resources.Facts." + Meta + ".json";

		/// <summary>One card definition, before the overlay rules and Card's preprocessing.</summary>
		internal sealed class Entity
		{
			public string Id;
			public int AssetId;
			public Tag[] Tags;
			public Dictionary<PlayReq, int> Requirements;
			public string[] Entourage;
			public Tag[] ReferencedTags;
		}

		internal sealed class Document
		{
			public string Meta;
			public Entity[] Cards;
			public ClientOverlay.Document Overlay;
		}

		internal static readonly Document Embedded = Read(typeof(CardFacts).Assembly.GetManifestResourceStream(Resource), Meta);

		internal static Document Read(Stream stream, string meta)
		{
			if (stream == null) throw new InvalidDataException("Missing card file for meta " + meta);
			using (stream)
			using (var bytes = new MemoryStream())
			{
				stream.CopyTo(bytes);
				return new Reader(Encoding.UTF8.GetString(bytes.GetBuffer(), 0, (int)bytes.Length)).ReadDocument(meta);
			}
		}

		private sealed class Reader
		{
			private readonly string _json;
			private int _at;
			private readonly List<Tag> _tags = new List<Tag>(32);

			internal Reader(string json) { _json = json; }

			internal Document ReadDocument(string meta)
			{
				var result = new Document();
				int schema = 0;
				Expect('{');
				do
				{
					string key = Key();
					switch (key)
					{
						case "schemaVersion": schema = Int(); break;
						case "meta": result.Meta = Str(); break;
						case "cards":
							var cards = new List<Entity>(8192);
							ReadArray(() => cards.Add(ReadRow(null)));
							result.Cards = cards.ToArray();
							break;
						case "overlay": result.Overlay = ReadOverlay(); break;
						default: throw Bad("unknown key " + key);
					}
				} while (Next('}'));
				SkipSpace();
				if (_at != _json.Length) throw Bad("trailing data");
				if (schema != 1 || result.Meta != meta || result.Cards == null || result.Overlay == null)
					throw new InvalidDataException("Invalid card file header for meta " + meta);
				return result;
			}

			private ClientOverlay.Document ReadOverlay()
			{
				var result = new ClientOverlay.Document();
				Expect('{');
				do
				{
					string key = Key();
					switch (key)
					{
						case "schemaVersion": result.SchemaVersion = Int(); break;
						case "sourceBuild": result.SourceBuild = Str(); break;
						case "exportSha256": result.ExportSha256 = Str(); break;
						case "cards":
							var rows = new List<ClientOverlay.Definition>(2560);
							ReadArray(() =>
							{
								var definition = new ClientOverlay.Definition();
								ReadRow(definition);
								rows.Add(definition);
							});
							result.Cards = rows.ToArray();
							break;
						default: throw Bad("unknown key " + key);
					}
				} while (Next('}'));
				return result;
			}

			// With a definition, also fills the overlay fields that ClientOverlay.Read takes from the XML.
			private Entity ReadRow(ClientOverlay.Definition definition)
			{
				var entity = new Entity();
				string name = null, text = null;
				bool hasAsset = false;
				Tag[] tags = null;
				Expect('{');
				do
				{
					string key = Key();
					switch (key)
					{
						case "id": entity.Id = Str(); break;
						case "dbf": entity.AssetId = Int(); hasAsset = true; break;
						case "name": name = Str(); break;
						case "text": text = Str(); break;
						case "textHash": SkipString(); break; // For the client drift check only.
						case "tags": tags = Pairs(); break;
						case "refs": entity.ReferencedTags = Pairs(); break;
						case "reqs":
							entity.Requirements = new Dictionary<PlayReq, int>();
							foreach (Tag pair in Pairs()) entity.Requirements.Add((PlayReq)(int)pair.GameTag, pair.TagValue);
							break;
						case "entourage":
							var ids = new List<string>();
							ReadArray(() => ids.Add(Str()));
							entity.Entourage = ids.ToArray();
							break;
						case "originalId" when definition != null: definition.OriginalId = Str(); break;
						case "originalDbf" when definition != null: definition.OriginalDbfId = Int(); break;
						case "snapshot" when definition != null: definition.ScopedSnapshot = True(); break;
						default: throw Bad("unknown key " + key);
					}
				} while (Next('}'));
				if (entity.Id == null || !hasAsset || tags == null) throw Bad("incomplete card row");

				// String tags have no order dependence: Card only keeps them as Name and Text.
				int count = tags.Length + (name != null ? 1 : 0) + (text != null ? 1 : 0);
				var all = new Tag[count];
				Array.Copy(tags, all, tags.Length);
				int at = tags.Length;
				if (name != null) all[at++] = new Tag(GameTag.CARDNAME, name);
				if (text != null) all[at] = new Tag(GameTag.CARDTEXT, text);
				entity.Tags = all;

				if (definition != null)
				{
					if (name == null || text == null) throw Bad("overlay row needs name and text: " + entity.Id);
					definition.CardId = entity.Id;
					definition.DbfId = entity.AssetId;
					definition.Name = name;
					definition.Text = text;
					definition.Tags = TagKeys(tags);
					definition.ReferencedTags = TagKeys(entity.ReferencedTags ?? Array.Empty<Tag>());
					definition.Entity = entity;
				}
				return entity;
			}

			private static Dictionary<string, int> TagKeys(Tag[] tags)
			{
				var keys = new Dictionary<string, int>(tags.Length);
				foreach (Tag tag in tags) keys.Add(((int)tag.GameTag).ToString(CultureInfo.InvariantCulture), tag.TagValue);
				return keys;
			}

			private Tag[] Pairs()
			{
				_tags.Clear();
				ReadArray(() =>
				{
					Expect('[');
					int key = Int();
					Expect(',');
					int value = Int();
					Expect(']');
					_tags.Add(new Tag((GameTag)key, value));
				});
				return _tags.ToArray();
			}

			private void ReadArray(Action item)
			{
				Expect('[');
				SkipSpace();
				if (Peek() == ']') { _at++; return; }
				do item(); while (Next(']'));
			}

			private string Key()
			{
				string key = Str();
				Expect(':');
				return key;
			}

			// After an item: true when a comma follows, false on the closing character.
			private bool Next(char close)
			{
				SkipSpace();
				char c = Peek();
				_at++;
				if (c == ',') return true;
				if (c == close) return false;
				throw Bad("expected , or " + close);
			}

			private void Expect(char c)
			{
				SkipSpace();
				if (Peek() != c) throw Bad("expected " + c);
				_at++;
			}

			private char Peek()
			{
				if (_at >= _json.Length) throw Bad("unexpected end");
				return _json[_at];
			}

			private void SkipSpace()
			{
				while (_at < _json.Length)
				{
					char c = _json[_at];
					if (c != ' ' && c != '\n' && c != '\r' && c != '\t') return;
					_at++;
				}
			}

			private int Int()
			{
				SkipSpace();
				bool negative = Peek() == '-';
				if (negative) _at++;
				int start = _at;
				long value = 0;
				while (_at < _json.Length && _json[_at] >= '0' && _json[_at] <= '9')
				{
					value = value * 10 + (_json[_at++] - '0');
					if (value > 1L + int.MaxValue) throw Bad("integer out of range");
				}
				if (_at == start) throw Bad("expected integer");
				value = negative ? -value : value;
				if (value > int.MaxValue) throw Bad("integer out of range");
				return (int)value;
			}

			private bool True()
			{
				SkipSpace();
				if (string.CompareOrdinal(_json, _at, "true", 0, 4) != 0) throw Bad("expected true");
				_at += 4;
				return true;
			}

			private void SkipString()
			{
				Expect('"');
				while (Peek() != '"') _at += _json[_at] == '\\' ? 2 : 1;
				_at++;
			}

			private string Str()
			{
				Expect('"');
				int start = _at;
				// Most values have no escapes; the rest fall through to the builder.
				while (true)
				{
					char c = Peek();
					if (c == '"') return _json.Substring(start, _at++ - start);
					if (c == '\\') break;
					_at++;
				}
				var result = new StringBuilder(_json, start, _at - start, Math.Max(16, (_at - start) * 2));
				while (true)
				{
					char c = Peek();
					_at++;
					if (c == '"') return result.ToString();
					if (c != '\\') { result.Append(c); continue; }
					c = Peek();
					_at++;
					switch (c)
					{
						case '"': case '\\': case '/': result.Append(c); break;
						case 'b': result.Append('\b'); break;
						case 'f': result.Append('\f'); break;
						case 'n': result.Append('\n'); break;
						case 'r': result.Append('\r'); break;
						case 't': result.Append('\t'); break;
						case 'u':
							if (_at + 4 > _json.Length || !int.TryParse(_json.Substring(_at, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int unit))
								throw Bad("invalid unicode escape");
							result.Append((char)unit);
							_at += 4;
							break;
						default: throw Bad("invalid escape");
					}
				}
			}

			private InvalidDataException Bad(string message) => new InvalidDataException("Invalid card file at " + _at + ": " + message);
		}
	}
}
