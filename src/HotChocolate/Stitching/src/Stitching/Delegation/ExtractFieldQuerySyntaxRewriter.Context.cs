using System.Collections.Generic;
using System.Collections.Immutable;
using HotChocolate.Language;
using HotChocolate.Language.Visitors;
using HotChocolate.Types;

namespace HotChocolate.Stitching.Delegation;

public partial class ExtractFieldQuerySyntaxRewriter
{
    public class Context : ISyntaxVisitorContext
    {
        public Context(
            string schema,
            INamedOutputType? typeContext,
            DocumentNode? document,
            OperationDefinitionNode? operation)
        {
            Schema = schema;
            Variables = new Dictionary<string, VariableDefinitionNode>();
            Document = document;
            Operation = operation;
            TypeContext = typeContext;
            Fragments = new Dictionary<string, FragmentDefinitionNode>();
            FragmentPath = ImmutableHashSet<string>.Empty;
        }

        public string Schema { get; }

        public DocumentNode? Document { get; }

        public OperationDefinitionNode? Operation { get; }

        public IDictionary<string, VariableDefinitionNode> Variables { get; }

        public INamedOutputType? TypeContext { get; set; }

        public IOutputField? OutputField { get; set; }

        public IInputField? InputField { get; set; }

        public IInputType? InputType { get; set; }

        public ImmutableHashSet<string> FragmentPath { get; set; }

        public IDictionary<string, FragmentDefinitionNode> Fragments { get; }

        public Context Clone()
        {
            return (Context)MemberwiseClone();
        }
    }
}
