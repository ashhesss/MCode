using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCode
{
    public class CSharpMetricCalculator : IMetricCalculator
    {
        private readonly HashSet<string> _operators = new();
        private readonly HashSet<string> _operands = new();
        private int _N1, _N2;

        public void Calculate(string sourceCode)
        {
            _operators.Clear();
            _operands.Clear();
            _N1 = 0;
            _N2 = 0;

            var tree = CSharpSyntaxTree.ParseText(sourceCode);
            var root = tree.GetRoot();

            foreach (var node in root.DescendantNodes().OfType<BinaryExpressionSyntax>())
            {
                _operators.Add(node.OperatorToken.Text);
                _N1++;
            }

            foreach (var node in root.DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                _operands.Add(node.Identifier.Text);
                _N2++;
            }
        }

        public MetricResult GetResults()
        {
            return new MetricResult
            {
                N1 = _N1,
                N2 = _N2,
                n1 = _operators.Count,
                n2 = _operands.Count
            };
        }
    }
}
