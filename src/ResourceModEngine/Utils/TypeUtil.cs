using AssetsTools.NET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ResourceModLoader.Utils
{
    internal class TypeUtil
    {
        public static TypeTreeType CloneTypeTree(TypeTreeType source)
        {
            var clone = new TypeTreeType
            {
                TypeId = source.TypeId,
                IsStrippedType = source.IsStrippedType,
                ScriptTypeIndex = source.ScriptTypeIndex,
                ScriptIdHash = source.ScriptIdHash,
                TypeHash = source.TypeHash,
                IsRefType = source.IsRefType,
                StringBuffer = source.StringBuffer,
                StringBufferBytes = source.StringBufferBytes?.ToArray(),
                TypeDependencies = source.TypeDependencies?.ToArray(),
                TypeReference = source.TypeReference != null
                    ? new AssetTypeReference(source.TypeReference.ClassName, source.TypeReference.Namespace, source.TypeReference.AsmName)
                    : null,
                Nodes = new List<TypeTreeNode>(),
            };
            foreach (var node in source.Nodes)
            {
                clone.Nodes.Add(new TypeTreeNode
                {
                    Version = node.Version,
                    Level = node.Level,
                    TypeFlags = node.TypeFlags,
                    TypeStrOffset = node.TypeStrOffset,
                    NameStrOffset = node.NameStrOffset,
                    ByteSize = node.ByteSize,
                    Index = node.Index,
                    MetaFlags = node.MetaFlags,
                    RefTypeHash = node.RefTypeHash,
                });
            }
            return clone;
        }
    }
}
