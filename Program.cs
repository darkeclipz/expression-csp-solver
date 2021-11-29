using System;
using System.Collections.Generic;
using System.Linq;

namespace LinearExprSolver
{
    class Program
    {
        static void Main(string[] _)
        {
            NQueens();
        }

        // https://developers.google.com/optimization/cp/queens#c_6
        static void NQueens()
        {
            // Create a domain that indicates in which row the queen is placed.
            Domain domain = new(0, 1, 2, 3, 4, 5, 6, 7);
            int boardSize = 8;

            // Create all the queens on the board.
            var queens = new VariableExpression[boardSize];
            for (int i = 0; i < boardSize; i++)
            {
                queens[i] = Expression.Variable($"Q{i}", domain);
            }

            CspModel model = new();

            // Add a constraint so that the queens cannot be in the same columns.
            model.AddAllDifferent(queens);

            // Add constraints so that the queens cannot be in the same diagonal.
            var diag1 = new IntegerExpression[boardSize];
            var diag2 = new IntegerExpression[boardSize];

            for (int i = 0; i < boardSize; ++i)
            {
                diag1[i] = Expression.Add(queens[i], Expression.Constant(i));
                diag2[i] = Expression.Add(queens[i], Expression.Constant(-i));
            }

            model.AddAllDifferent(diag1);
            model.AddAllDifferent(diag2);

            model.PrintModel();

            CspSolver solver = new();
            bool solved = solver.Solve(model);
            Console.WriteLine($"Model solved: {solved}");

            model.PrintModel();
        }

        //static void AllDifferentConstraintTest()
        //{
        //    Domain domain = new(0, 1, 2, 3, 4, 5, 6, 7);
        //    var a = Expression.Variable("a", domain);
        //    var b = Expression.Variable("b", domain);
        //    var c = Expression.Variable("c", domain);

        //    a.Set(1);
        //    b.Set(2);
        //    c.Set(3);

        //    var expr = Expression.AllDifferent(a, b, c);
        //    Console.WriteLine($"{expr} = {expr.Evaluate()}");
        //}
    }

    class CspSolver
    {
        private CspModel Model { get; set; }
        private Dictionary<string, VariableExpression> Variables { get; set; } = new();

        public bool Solve(CspModel model)
        {
            Console.WriteLine("Solving...");

            Model = model;
            FindVariables();
            return Backtrack();
        }

        private void FindVariables()
        {
            foreach(var constraint in Model.Constraints)
            {
                foreach(var variable in constraint.EnumerateVariables())
                {
                    if(!Variables.ContainsKey(variable.Name))
                    {
                        Variables.Add(variable.Name, variable);
                    }
                }
            }
        }

        private VariableExpression NextVariable()
        {
            foreach(var (_, variable) in Variables)
            {
                if(!variable.Assigned)
                {
                    return variable;
                }
            }

            return null;
        }

        private bool Backtrack()
        {
            VariableExpression variable = NextVariable();
            if(variable == null)
            {
                // All the variables have been assigned.
                return IsSatisfied();
            }

            foreach(var value in variable.Variable.Domain.Values)
            {
                Assign(variable, value);
                if(Backtrack())
                {
                    return true;
                }
                Unassign(variable);
            }

            return false;
        }

        private static void Assign(VariableExpression variable, int value)
        {
            // Console.WriteLine($"ASSIGN({variable.Name} = {value})");
            variable.Set(value);
        }

        private static void Unassign(VariableExpression variable)
        {
            // Console.WriteLine($"UNASSIGN({variable.Name})");
            variable.Value = default;
            variable.Assigned = false;
        }

        private bool IsSatisfied()
        {
            foreach(var constraint in Model.Constraints)
            {
                if(constraint.HasAllVariablesAssigned() && !constraint.Evaluate())
                {
                    return false;
                }
            }

            return true;
        }
    }

    class CspModel
    {
        public List<BooleanExpression> Constraints { get; set; } = new();

        public AllDifferentExpression AddAllDifferent(params IntegerExpression[] expression)
        {
            var expr = Expression.AllDifferent(expression); ;
            Constraints.Add(expr);
            return expr;
        }

        public void PrintModel()
        {
            foreach(var constraint in Constraints)
            {
                Console.WriteLine(constraint);
            }
        }
    }

    class Domain
    {
        public List<int> Values { get; set; }

        public Domain(params int[] values)
        {
            Values = values.ToList();
        }

        public Domain Copy()
        {
            return new Domain(Values.ToArray());
        }
    }

    class Variable
    {
        public Variable(string name, Domain domain)
        {
            Name = name;
            Domain = domain.Copy();
        }

        public string Name { get; set; }
        public bool IsSet { get; set; }
        public int Value { get; set; }
        public Domain Domain { get; set; }
    }

    // a + b + c + d <= 5      a, b, c, d \in { 1, 2, 3, 4 }
    // AllDiff(a, b, c)        Ai != Aj,   i != j     =>     a != b, a != c, b != c.    NotEqual(a, b)

    abstract class Expression
    {
        public virtual IEnumerable<Expression> Traverse()
        {
            throw new NotImplementedException();
        }

        public bool HasAllVariablesAssigned()
        {
            foreach (var node in Traverse())
            {
                if (node is VariableExpression variable)
                {
                    if (!variable.Assigned)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public IEnumerable<VariableExpression> EnumerateVariables()
        {
            foreach(var expr in Traverse())
            {
                if(expr is VariableExpression variable)
                {
                    yield return variable;
                }
            }
        }

        public static ConstantExpression Constant(int value) => new(value);
        public static AddExpression Add(IntegerExpression left, IntegerExpression right) => new(left, right);
        public static VariableExpression Variable(string name, Domain domain) => new(name, domain);
        public static EqualityExpression Equals(IntegerExpression left, IntegerExpression right) => new(left, right);
        public static NotExpression Not(BooleanExpression expr) => new(expr);
        public static AllDifferentExpression AllDifferent(params IntegerExpression[] expressions) => new(expressions);
    }

    abstract class BooleanExpression : Expression
    {
        public virtual bool Evaluate()
        {
            throw new NotImplementedException();
        }

    }

    abstract class IntegerExpression : Expression
    {
        public virtual int Evaluate()
        {
            throw new NotImplementedException();
        }
    }

    class ConstantExpression : IntegerExpression
    {
        public int Value { get; }

        public ConstantExpression(int value)
        {
            Value = value;
        }

        public override int Evaluate()
        {
            return Value;
        }

        public override IEnumerable<Expression> Traverse()
        {
            yield return this;
        }

        public override string ToString()
        {
            return Value.ToString();
        }
    }

    class VariableExpression : IntegerExpression
    {
        public new Variable Variable { get; set; }

        public string Name
        {
            get => Variable.Name;
        }

        public bool Assigned
        {
            get => Variable.IsSet;
            set => Variable.IsSet = value;
        }
            
        public int Value
        {
            get => Variable.Value;
            set => Variable.Value = value;
        }

        public VariableExpression(string name, Domain domain)
        {
            Variable = new Variable(name, domain);
        }

        public void Set(int value)
        {
            Value = value;
            Assigned = true;
        }

        public override int Evaluate()
        {
            return Value;
        }

        public override IEnumerable<Expression> Traverse()
        {
            yield return this;
        }

        public override string ToString()
        {
            // Ternary expression
            return Assigned
                ? Value.ToString()
                : Name;
        }
    }

    class AddExpression : IntegerExpression
    {
        public AddExpression(IntegerExpression left, IntegerExpression right)
        {
            Left = left;
            Right = right;
        }

        public IntegerExpression Left { get; }

        public IntegerExpression Right { get; }

        public override int Evaluate()
        {
            return Left.Evaluate() + Right.Evaluate();
        }

        public override IEnumerable<Expression> Traverse()
        {
            yield return this;

            foreach(var expression in Left.Traverse())
            {
                yield return expression;
            }

            foreach(var expression in Right.Traverse())
            {
                yield return expression;
            }
        }

        public override string ToString()
        {
            if(Right.Evaluate() < 0)
            {
                return $"{Left}+({Right})";
            }
            return $"{Left}+{Right}";
        }
    }

    class EqualityExpression : BooleanExpression
    {
        public EqualityExpression(IntegerExpression left, IntegerExpression right)
        {
            Left = left;
            Right = right;
        }

        public IntegerExpression Left { get; }

        public IntegerExpression Right { get; }

        public override bool Evaluate()
        {
            return Left.Evaluate() == Right.Evaluate();
        }

        public override IEnumerable<Expression> Traverse()
        {
            yield return this;

            foreach (var expression in Left.Traverse())
            {
                yield return expression;
            }

            foreach (var expression in Right.Traverse())
            {
                yield return expression;
            }
        }

        public override string ToString()
        {
            return $"{Left} == {Right}";
        }
    }

    class NotExpression : BooleanExpression
    {
        public NotExpression(BooleanExpression expression)
        {
            Expression = expression;
        }

        public BooleanExpression Expression { get; }

        public override bool Evaluate()
        {
            return !Expression.Evaluate();
        }

        public override IEnumerable<Expression> Traverse()
        {
            yield return this;
            yield return Expression;
        }

        public override string ToString()
        {
            return $"!({Expression})";
        }
    }

    class AllDifferentExpression : BooleanExpression
    {
        private List<IntegerExpression> Expressions { get; set; }

        public AllDifferentExpression(params IntegerExpression[] expressions)
        {
            Expressions = expressions.ToList();
        }

        public override bool Evaluate()
        {
            Dictionary<int, bool> compare = new();
            foreach(var expr in Expressions)
            {
                int value = expr.Evaluate();
                if(compare.ContainsKey(value))
                {
                    return false;
                }

                compare.Add(value, true);
            }

            return true;
        }

        public override IEnumerable<Expression> Traverse()
        {
            yield return this;
            foreach(var expression in Expressions)
            {
                foreach(var expr in expression.Traverse())
                {
                    yield return expr;
                }
            }
        }

        public override string ToString()
        {
            return $"AllDiff({string.Join(", ", Expressions)})";
        }
    }
}
