using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ParamsArrayCallInLoop
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ParamsArrayCallInLoopAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "ParamsArrayCallInLoop";
        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.AnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.AnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = "Performance";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeSymbol, SyntaxKind.InvocationExpression);
        }

        static bool IsInSyntax<T>(SyntaxNode syntax) where T : class
        {
            do
            {
                if (syntax.Parent is LocalFunctionStatementSyntax || syntax.Parent is AnonymousFunctionExpressionSyntax || syntax.Parent is MethodDeclarationSyntax)
                    return false;
                if (syntax.Parent is T)
                    return true;
                syntax = syntax.Parent;
            } while (syntax.Parent != null);
            return false;
        }

        private static void AnalyzeSymbol(SyntaxNodeAnalysisContext context)
        {
            var invocation = context.Node as InvocationExpressionSyntax;
            if (!(context.SemanticModel.GetSymbolInfo(invocation).Symbol is IMethodSymbol methodSymbol))
                return;

            if (!methodSymbol.ContainingType.ContainingNamespace.Name.Equals("System"))
                return;

            if (methodSymbol.Parameters.Any() && methodSymbol.Parameters.Last().IsParams && (
                IsInSyntax<ForEachStatementSyntax>(invocation) 
                || IsInSyntax<ForStatementSyntax>(invocation)))
            {
                if (context.SemanticModel.GetTypeInfo(invocation.ArgumentList.Arguments.Last().Expression).Type.Kind == SymbolKind.ArrayType 
                    && invocation.ArgumentList.Arguments.Count == methodSymbol.Parameters.Length
                    && !(invocation.ArgumentList.Arguments.Last().Expression is ArrayCreationExpressionSyntax)) // Array is used as params parameter
                    return;
                // For all such symbols, produce a diagnostic.
                var diagnostic = Diagnostic.Create(Rule, invocation.GetLocation(), invocation.Expression);

                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
