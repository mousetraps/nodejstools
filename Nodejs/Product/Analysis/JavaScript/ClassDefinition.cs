using System;
using System.Collections.Generic;

namespace Microsoft.NodejsTools.Parsing
{
    internal class ClassDefinition : Statement
    {
        private string className;
        private List<Statement> members;

        public ClassDefinition(EncodedSpan encodedSpan, string className, List<Statement> members) : base(encodedSpan)
        {
            this.className = className;
            this.members = members;
        }

        public override IEnumerable<Node> Children
        {
            get
            {
                return members;
            }
        }

        public override void Walk(AstVisitor walker)
        {
            if (walker.Walk(this))
            {
                foreach (var member in members)
                {
                    member.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }
    }
}