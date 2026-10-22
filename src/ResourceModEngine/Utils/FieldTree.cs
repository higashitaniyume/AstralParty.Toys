using AssetsTools.NET;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;

namespace ResourceModLoader.Utils
{
    public readonly struct FieldTree
    {
        private readonly AssetTypeValueField _field;

        public FieldTree(AssetTypeValueField field)
        {
            _field = field ?? AssetTypeValueField.DUMMY_FIELD;
        }

        public AssetTypeValueField Root => _field;

        public bool IsDummy => _field.IsDummy;

        public AssetTypeTemplateField TemplateField
        {
            get => _field.TemplateField;
            set => _field.TemplateField = value;
        }

        public FieldTree this[string name] => new FieldTree(_field[name]);
        public FieldTree this[int index] => new FieldTree(_field[index]);

        public List<FieldTree> Children
        {
            get
            {
                var children = _field.Children;
                if (children == null || children.Count == 0)
                    return new List<FieldTree>();
                var result = new List<FieldTree>(children.Count);
                foreach (var child in children)
                    result.Add(new FieldTree(child));
                return result;
            }
        }

        public string AsString { get => _field.AsString; set => _field.AsString = value; }
        public int AsInt { get => _field.AsInt; set => _field.AsInt = value; }
        public long AsLong { get => _field.AsLong; set => _field.AsLong = value; }
        public ulong AsULong { get => _field.AsULong; set => _field.AsULong = value; }
        public byte[] AsByteArray { get => _field.AsByteArray; set => _field.AsByteArray = value; }
        public bool AsBool { get => _field.AsBool; set => _field.AsBool = value; }
        public float AsFloat { get => _field.AsFloat; set => _field.AsFloat = value; }
        public double AsDouble { get => _field.AsDouble; set => _field.AsDouble = value; }
        public uint AsUInt { get => _field.AsUInt; set => _field.AsUInt = value; }
        public short AsShort { get => _field.AsShort; set => _field.AsShort = value; }
        public ushort AsUShort { get => _field.AsUShort; set => _field.AsUShort = value; }
        public byte AsByte { get => _field.AsByte; set => _field.AsByte = value; }
        public sbyte AsSByte { get => _field.AsSByte; set => _field.AsSByte = value; }

        public FieldTree Clone()
        {
            return new FieldTree(_field.Clone());
        }

        public static implicit operator FieldTree(AssetTypeValueField field)
        {
            return new FieldTree(field);
        }

        public static void CopyValues(FieldTree source, FieldTree target)
        {
            CopyValuesInternal(source._field, target._field);
        }

        private static void CopyValuesInternal(AssetTypeValueField source, AssetTypeValueField target)
        {
            if (source.IsDummy || target.IsDummy)
                return;

            var sChildren = source.Children;
            var tChildren = target.Children;
            bool sHasChildren = sChildren != null && sChildren.Count > 0;
            bool tHasChildren = tChildren != null && tChildren.Count > 0;

            if (sHasChildren && tHasChildren)
            {
                for(int i = 0; i < sChildren!.Count; i++)
                {
                    string fieldName = sChildren[i].FieldName;
                    for(int j = 0; j < tChildren!.Count; j++)
                    {
                        if (tChildren[j].FieldName == fieldName)
                        {
                            CopyValuesInternal(sChildren[i], tChildren[j]);
                            break;
                        }
                    }
                }
            }
            else if (!sHasChildren && !tHasChildren)
            {
                if (source.Value.ValueType == AssetValueType.ManagedReferencesRegistry)
                {
                    var sv = source.Value.AsManagedReferencesRegistry;
                    List<AssetTypeReferencedObject> references = new List<AssetTypeReferencedObject>();
                    for (int i = 0; i < sv.references.Count; i++)
                    {
                        AssetTypeReferencedObject origReference = sv.references[i];
                        references.Add(new AssetTypeReferencedObject
                        {
                            rid = origReference.rid,
                            type = new AssetTypeReference
                            {
                                ClassName = origReference.type.ClassName,
                                Namespace = origReference.type.Namespace,
                                AsmName = origReference.type.AsmName
                            },
                            data = origReference.data?.Clone()
                        });
                    }

                    target.Value = new AssetTypeValue(source.Value.ValueType, new ManagedReferencesRegistry
                    {
                        version = sv.version,
                        references = references
                    });
                }
                else
                {
                    target.Value = source.Value.Clone();
                }
            }
        }

        public static bool IsSame(FieldTree a, FieldTree b)
        {
            return CompareInternal(a._field, b._field);
        }

        private static bool CompareInternal(AssetTypeValueField a, AssetTypeValueField b)
        {
            if (a.IsDummy && b.IsDummy)
                return true;

            if (a.IsDummy || b.IsDummy)
            {
                return false;
            }

            var aChildren = a.Children;
            var bChildren = b.Children;
            bool aHasChildren = aChildren != null && aChildren.Count > 0;
            bool bHasChildren = bChildren != null && bChildren.Count > 0;

            if (aHasChildren && bHasChildren)
            {
                if (aChildren!.Count != bChildren!.Count)
                {
                    return false;
                }

                for (int i = 0; i < aChildren.Count; i++)
                {
                    var childA = aChildren[i];
                    var childB = bChildren[i];
                    if(!CompareInternal(childA, childB))
                        return false;
                }
                return true;
            }
            else if (!aHasChildren && !bHasChildren)
            {
                if (a.Value == null && b.Value == null)
                    return true;
                if (a.Value == null || b.Value == null)
                {
                    return false;
                }
                if (a.Value.ValueType != b.Value.ValueType)
                {
                    return false;
                }

                if (!ValuesEqual(a, b)) 
                    return false;
                return true;
            }
            else
            {
                return false;
            }
        }

        private static bool ValuesEqual(AssetTypeValueField a, AssetTypeValueField b)
        {
            switch (a.Value.ValueType)
            {
                case AssetValueType.Bool:
                    return a.AsBool == b.AsBool;
                case AssetValueType.Int8:
                    return a.AsSByte == b.AsSByte;
                case AssetValueType.UInt8:
                    return a.AsByte == b.AsByte;
                case AssetValueType.Int16:
                    return a.AsShort == b.AsShort;
                case AssetValueType.UInt16:
                    return a.AsUShort == b.AsUShort;
                case AssetValueType.Int32:
                    return a.AsInt == b.AsInt;
                case AssetValueType.UInt32:
                    return a.AsUInt == b.AsUInt;
                case AssetValueType.Int64:
                    return a.AsLong == b.AsLong;
                case AssetValueType.UInt64:
                    return a.AsULong == b.AsULong;
                case AssetValueType.Float:
                    return a.AsFloat == b.AsFloat;
                case AssetValueType.Double:
                    return a.AsDouble == b.AsDouble;
                case AssetValueType.String:
                    return a.AsString == b.AsString;
                case AssetValueType.ByteArray:
                    var baA = a.AsByteArray;
                    var baB = b.AsByteArray;
                    if (baA == null && baB == null) return true;
                    if (baA == null || baB == null) return false;
                    return baA.SequenceEqual(baB);
                case AssetValueType.ManagedReferencesRegistry:
                    var rA = a.AsManagedReferencesRegistry;
                    var rB = b.AsManagedReferencesRegistry;
                    if( rA == null || rB == null || rA.version != rB.version) return false;
                    if(rA.references.Count != rB.references.Count) return false;
                    for(int i=0;i< rA.references.Count; i++)
                    {
                        var rrA = rA.references[i];
                        var rrB = rB.references[i];
                        if(rrA.rid != rrB.rid|| !rrA.type.Equals(rrB.type)) return false;
                        if(!CompareInternal(rrA.data, rrB.data)) return false;
                    }
                    return true;
                default:
                    return true;
            }
        }
    }
}
