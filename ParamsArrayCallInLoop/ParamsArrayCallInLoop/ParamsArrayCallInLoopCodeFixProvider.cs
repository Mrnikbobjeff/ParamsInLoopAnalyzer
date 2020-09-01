using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;
using System.Text;
using Microsoft.CodeAnalysis.Formatting;

namespace ParamsArrayCallInLoop
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ParamsArrayCallInLoopCodeFixProvider)), Shared]
    public class ParamsArrayCallInLoopCodeFixProvider : CodeFixProvider
    {
        private const string title = "Hoist assignment";

        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(ParamsArrayCallInLoopAnalyzer.DiagnosticId); }
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            // TODO: Replace the following code with your own analysis, generating a CodeAction for each fix to suggest
            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            // Find the type declaration identified by the diagnostic.
            var declaration = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<InvocationExpressionSyntax>().First();

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: title,
                    createChangedSolution: c => HoistAssignment(context.Document, declaration, c),
                    equivalenceKey: title),
                diagnostic);
        }
        static T IsInSyntax<T>(SyntaxNode syntax) where T : class
        {
            do
            {
                if (syntax.Parent is T)
                    return syntax.Parent as T;
                syntax = syntax.Parent;
            } while (syntax.Parent != null);
            return null;
        }
        private async Task<Solution> HoistAssignment(Document document, InvocationExpressionSyntax paramsInvocation, CancellationToken cancellationToken)
        {
            var originalSolution = document.Project.Solution;
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var method = semanticModel.GetSymbolInfo(paramsInvocation).Symbol as IMethodSymbol;
            var typeDisplayString = method.Parameters.Last().Type.ToMinimalDisplayString(semanticModel, method.Parameters.Last().Locations.First().SourceSpan.Start);

            var bracketedSyntax = SyntaxFactory.BracketedArgumentList();
            var updatedParameters = new SeparatedSyntaxList<ExpressionSyntax>();
            var actualArguments = paramsInvocation.ArgumentList.Arguments.Skip(method.Parameters.Length - 1).Select(x => x.Expression).ToArray();
            updatedParameters = updatedParameters.AddRange(actualArguments);
            var newArray = SyntaxFactory.InitializerExpression(SyntaxKind.ArrayInitializerExpression, updatedParameters);
            var typeSyntax = SyntaxFactory.ParseTypeName(typeDisplayString);
            var objectCreationExpression = SyntaxFactory.ObjectCreationExpression(typeSyntax, null, newArray).WithAdditionalAnnotations(Formatter.Annotation);
            var equalsValueClause = SyntaxFactory.EqualsValueClause(objectCreationExpression);
            var declarator = new SeparatedSyntaxList<VariableDeclaratorSyntax>();
            declarator = declarator.Add(SyntaxFactory.VariableDeclarator(SyntaxFactory.Identifier("hoisted"), null, equalsValueClause));
            var variableAssignment = SyntaxFactory.VariableDeclaration(typeSyntax, declarator).WithAdditionalAnnotations(Formatter.Annotation);
            var assignmentExpression = SyntaxFactory.LocalDeclarationStatement(variableAssignment);

            var docRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var forStatement = IsInSyntax<ForStatementSyntax>(paramsInvocation);
            SyntaxNode newRoot = null;
            if(forStatement != null)
            {
                newRoot = docRoot.InsertNodesBefore(forStatement, new[] { assignmentExpression });
                newRoot = Formatter.Format(newRoot, Formatter.Annotation, document.Project.Solution.Workspace);
            }
            return originalSolution.WithDocumentSyntaxRoot(document.Id, newRoot);
        }
    }
}
