using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using TestHelper;
using ParamsArrayCallInLoop;

namespace ParamsArrayCallInLoop.Test
{
    [TestClass]
    public class UnitTest : CodeFixVerifier
    {
        [TestMethod]
        public void EmptyText_NoDiagnostic()
        {
            var test = @"";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void ObjectParamsCall_SingleDiagnostic()
        {
            var test = @"
    using System;

    namespace ConsoleApplication1
    {
        class TypeName
        {   
            public void Test()
            {
                for(int i = 0; i < 100; i++)
                    String.Format("""", 1,2,3,4,5,6,7,8,9,0);
            }
        }
    }";
            var expected = new DiagnosticResult
            {
                Id = "ParamsArrayCallInLoop",
                Message = String.Format("Method invocation parameters '{0}' should be hoisted", "String.Format"),
                Severity = DiagnosticSeverity.Warning,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 11, 21)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);
        }
        [TestMethod]
        public void ObjectParamsCall_LocalFunc_NoDiagnostic()
        {
            var test = @"
    using System;

    namespace ConsoleApplication1
    {
        class TypeName
        {   
            public void Test()
            {
                for(int i = 0; i < 100; i++)
                {
                    void inner() {String.Format("""", 1,2,3,4,5,6,7,8,9,0);}
                    inner();
                };
            }
        }
    }";
            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void ObjectParamsCall_SingleFix()
        {
            var test = @"
    using System;

    namespace ConsoleApplication1
    {
        class TypeName
        {   
            public void Test()
            {
                for(int i = 0; i < 100; i++)
                    String.Format("""", 1,2,3,4,5,6,7,8,9,0);
            }
        }
    }";

            var fixtest = @"
    using System;

    namespace ConsoleApplication1
    {
        class TypeName
        {   
            public void Test()
            {
                var hoisted = new object[]{1,2,3,4,5,6,7,8,9,0};
                for(int i = 0; i < 100; i++)
                    String.Format("""", hoisted);
            }
        }
    }";
            VerifyCSharpFix(test, fixtest);
        }

        protected override CodeFixProvider GetCSharpCodeFixProvider()
        {
            return new ParamsArrayCallInLoopCodeFixProvider();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new ParamsArrayCallInLoopAnalyzer();
        }
    }
}
