#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Tokens;
using PolishConverter;

namespace PolishConverter {
  public static class Converter {
    public static List<Token> ConvertToPolishNotation(List<Token> expression) {
      var operatorsStack = new Stack<Token>();
      var outputQueue = new Queue<Token>();

      foreach (var token in expression) {
        switch (token.Type) {
          case TokenType.Constant:
          case TokenType.Variable:
            outputQueue.Enqueue(token);
            break;
          case TokenType.OpenBracket:
            operatorsStack.Push(token);
            break;
          case TokenType.ClosedBracket: {
            while (operatorsStack.Count > 0 && operatorsStack.Peek().Type != TokenType.OpenBracket) {
              var op = operatorsStack.Pop();
              outputQueue.Enqueue(op);
            }

            if (operatorsStack.Count == 0) {
              throw new Exception("Mismatched parentheses in expression.");
            }

            operatorsStack.Pop(); // Remove '(' from stack
            break;
          }
          case TokenType.Add:
          case TokenType.Sub:
          case TokenType.Mult:
          case TokenType.Div:
          case TokenType.Rem:
          case TokenType.Pow: {
            var precedence = OperatorPrecedence(token);
            while (operatorsStack.Count > 0 && operatorsStack.Peek().Type != TokenType.OpenBracket &&
                   OperatorPrecedence(operatorsStack.Peek()) >= precedence) {
              var op = operatorsStack.Pop();
              outputQueue.Enqueue(op);
            }

            operatorsStack.Push(token);
            break;
          }
          default:
            throw new Exception("Invalid token in expression.");
        }
      }

      while (operatorsStack.Count > 0) {
        var op = operatorsStack.Pop();
        if (op.Type == TokenType.OpenBracket) {
          throw new Exception("Mismatched parentheses in expression.");
        }

        outputQueue.Enqueue(op);
      }

      var outputArray = new List<Token>();
      while (outputQueue.Count != 0) {
        outputArray.Add(outputQueue.Dequeue());
      }

      return outputArray;
    }

    private static int OperatorPrecedence(Token op) {
      switch (op.Type) {
        case TokenType.Add:
        case TokenType.Sub:
          return 1;
        case TokenType.Mult:
        case TokenType.Div:
        case TokenType.Rem:
          return 2;
        case TokenType.Pow:
          return 3;
        case TokenType.Variable:
        case TokenType.Constant:
        case TokenType.OpenBracket:
        case TokenType.ClosedBracket:
        default:
          throw new ArgumentException($"Invalid operator: {op}");
      }
    }
  }
}


namespace Tokens {
  public enum TokenType {
    Variable, // like a, b, abc
    Constant, // 12, 34, 231
    Add, // +
    Sub, // -
    Mult, // *
    Div, // /
    Pow, // ^
    Rem, // %
    OpenBracket, // ( (used only in Tokenizer and PolishConverter)
    ClosedBracket, // ) (used only in Tokenizer and PolishConverter)
  }

  public class Token {
    public TokenType Type { get; }
    public string? Value { get; }

    public Token(TokenType t, string? val) {
      Type = t;
      Value = val;
    }
  }
}


namespace Tree {
  public class TreeNode {
    public Token Token { get; }
    public TreeNode? Left;
    public TreeNode? Right;

    public TreeNode(Token token) {
      Token = token;
    }

    public string? Solve(in Dictionary<string, double> vars) {
      if ((Left == null || Right == null) && Token.Type is TokenType.Constant or TokenType.Variable) {
        if (Token.Type == TokenType.Constant || Token.Type == TokenType.Variable && vars.ContainsKey(Token.Value!)) {
          return Token.Type == TokenType.Constant
            ? Token.Value
            : vars[Token.Value!].ToString(CultureInfo.CurrentCulture);
        }
        throw new ArgumentException($"Missing var in vars dict: '{Token.Value}'");
      }

      var a = Convert.ToDouble(Left!.Solve(vars));
      var b = Convert.ToDouble(Right!.Solve(vars));
      var res = Token.Type switch {
        TokenType.Add => a + b,
        TokenType.Sub => a - b,
        TokenType.Mult => a * b,
        TokenType.Div => a / b,
        TokenType.Pow => Math.Pow(a, b),
        TokenType.Rem => a % b,
        TokenType.Variable => throw new ArgumentOutOfRangeException($"On this step we cant have var token"),
        TokenType.Constant => throw new ArgumentOutOfRangeException($"On this step we cant have const token"),
        TokenType.OpenBracket => throw new ArgumentOutOfRangeException($"On this step we cant have '(' token"),
        TokenType.ClosedBracket => throw new ArgumentOutOfRangeException($"On this step we cant have ')' token"),
        _ => throw new ArgumentOutOfRangeException($"Got wrong operator type, type: {Token.Type}")
      };

      return res.ToString(CultureInfo.InvariantCulture);
    }
  }

  public class Tree {
    private TreeNode? _root;
    private uint _varsCount;

    public Tree() {
      _root = null;
    }

    public void Build(in List<Token> tokenizedPolishNotation) {
      var s = new Stack<TreeNode>();
      var nodes = tokenizedPolishNotation.Select(token => new TreeNode(token)).ToList();

      foreach (var node in nodes) {
        switch (node.Token.Type) {
          case TokenType.Add:
          case TokenType.Sub:
          case TokenType.Mult:
          case TokenType.Div:
          case TokenType.Rem:
          case TokenType.Pow:
            var right = s.Pop();
            var left = s.Pop();
            var newNode = new TreeNode(node.Token) {
              Left = left,
              Right = right
            };
            s.Push(newNode);
            break;
          case TokenType.Constant:
            s.Push(node);
            break;
          case TokenType.Variable:
            _varsCount++;
            s.Push(node);
            break;
          case TokenType.OpenBracket:
          case TokenType.ClosedBracket:
          default:
            throw new ArgumentException($"Invalid node: {node.Token.Value}");
        }
      }

      if (s.Count != 1) {
        throw new ArgumentException($"Nodes stack are not empty, invalid polish notation");
      }

      _root = s.Pop();
    }

    public double Solve(Dictionary<string?, double>? values = null) {
      if (values != null && _varsCount > values.Count || values == null && _varsCount != 0) {
        throw new ArgumentException(
          $"Wrong count of variables are given: expected {_varsCount}, got {values?.Count ?? 0}");
      }

      var result = _root?.Solve(values!);
      return Convert.ToDouble(result);
    }
  }
}

namespace mai_sp {
  internal static class Program {
    private static List<Token> Tokenize(string inputExpression) {
      var result = new List<Token>();
      var tokens = new List<string>();

      for (var i = 0; i < inputExpression.Length; i++) {
        var c = inputExpression[i];
        if (char.IsDigit(c)) {
          // Token is a number
          var sb = new StringBuilder();
          sb.Append(c);
          while (i + 1 < inputExpression.Length && char.IsDigit(inputExpression[i + 1])) {
            sb.Append(inputExpression[i + 1]);
            i++;
          }

          tokens.Add(sb.ToString());
          result.Add(new Token(TokenType.Constant, sb.ToString()));
        } else if (char.IsLetter(c)) {
          // Token is a variable
          var sb = new StringBuilder();

          sb.Append(c);
          while (i + 1 < inputExpression.Length &&
                 (char.IsLetterOrDigit(inputExpression[i + 1]) || inputExpression[i + 1] == '_')) {
            sb.Append(inputExpression[i + 1]);
            i++;
          }

          tokens.Add(sb.ToString());
          result.Add(new Token(TokenType.Variable, sb.ToString()));
        } else if (c is '+' or '-' or '*' or '/' or '^' or '%' or '(' or ')') {
          tokens.Add(c.ToString());
          switch (c) {
            case ('+'):
              result.Add(new Token(TokenType.Add, c.ToString()));
              break;
            case ('-'):
              result.Add(new Token(TokenType.Sub, c.ToString()));
              break;
            case ('*'):
              result.Add(new Token(TokenType.Mult, c.ToString()));
              break;
            case ('/'):
              result.Add(new Token(TokenType.Div, c.ToString()));
              break;
            case ('^'):
              result.Add(new Token(TokenType.Pow, c.ToString()));
              break;
            case ('%'):
              result.Add(new Token(TokenType.Rem, c.ToString()));
              break;
            case ('('):
              result.Add(new Token(TokenType.OpenBracket, c.ToString()));
              break;
            case (')'):
              result.Add(new Token(TokenType.ClosedBracket, c.ToString()));
              break;
            default:
              throw new ArgumentOutOfRangeException();
          }
        }
      }

      foreach (var token in tokens) {
        Console.WriteLine(token);
      }

      return result;
    }

    public static void Main() {
      // const string expression = "(3 + 4) * 2 / (7 - 5) ^ 2 ^ 3"; // ERROR: 3 is var 
      const string expression = "(3 + a) * 2 / (b - 5) ^ 2 ^ 3";
      var tokenized = Tokenize(expression);

      var res = Converter.ConvertToPolishNotation(tokenized);
      Console.WriteLine(res);
      foreach (var token in res) {
        Console.Write($"{token.Value} ");
      }

      var tree = new Tree.Tree();
      tree.Build(res);
      Console.WriteLine();
      var parameters = new Dictionary<string, double> {
        ["a"] = 4,
        ["b"] = 7,
      };
      Console.WriteLine(tree.Solve(parameters!));
    }
  }
}