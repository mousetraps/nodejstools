using System;
using System.Collections.Generic;

namespace Microsoft.NodejsTools.Parsing
{
    [Serializable]
    internal class MethodDefinition : Statement
    {
        private Block body;
        private List<ParameterDeclaration> formalParameters;
        private Lookup functionName;
        private EncodedSpan paramSpan;

        public MethodDefinition(EncodedSpan functionSpan, List<ParameterDeclaration> formalParameters, Block body, EncodedSpan paramSpan, Lookup functionName) : base(functionSpan)
        {
            this.formalParameters = formalParameters;
            this.body = body;
            this.paramSpan = paramSpan;
            this.functionName = functionName;
        }

        public override void Walk(AstVisitor walker)
        {
            if (walker.Walk(this))
            {
                foreach (var param in formalParameters)
                {
                    param.Walk(walker);
                }

                body.Walk(walker);
            }

            walker.PostWalk(this);
        }

        public override IEnumerable<Node> Children
        {
            get
            {
                foreach (var param in formalParameters)
                {
                    yield return param;
                }

                yield return body;
            }
        }
    }
}