using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Limcap.LightValidator {

	/// <summary>
	///	Provides validation for any object and its members.
	/// </summary>
	public class Validator {
		public Validator(dynamic obj = null) {
			Object = obj;
		}



		// Current Param fields
		internal string _paramName;
		internal dynamic _paramValue;
		internal dynamic _paramEqualizer;
		internal bool _paramIsValid;
		internal ValidationResult _paramResult;



		public dynamic Object { get; private set; }
		public List<ValidationResult> Results { get; private set; }
		public string LastError => Results.LastOrDefault().ErrorMessages?.LastOrDefault();
		public bool LastTestHasPassed { get; internal set; }



		public void Reset(dynamic obj = null) {
			Object = obj;
			Results = null;
			_paramName = null;
			_paramValue = null;
			_paramEqualizer = null;
			_paramIsValid = false;
			_paramResult = new ValidationResult();
			LastTestHasPassed = true;
		}



		public Param<dynamic> Param(string paramNmae) => Param<dynamic>(paramNmae, null);



		public Param<V> Param<V>(string paramNmae, V paramValue) {
			_paramName = paramNmae;
			_paramValue = paramValue;
			_paramIsValid = true;
			_paramResult = new ValidationResult();
			_paramEqualizer = null;
			LastTestHasPassed = true;
			return new Param<V>(this);
		}



		internal void AddErrorMessage(
		string msg) {
			if (_paramIsValid) {
				_paramResult = new ValidationResult(_paramName);
				InitializeResults();
				Results.Add(_paramResult);
				_paramIsValid = false;
			}
			_paramResult.ErrorMessages.Add(msg);
		}






		internal void InitializeResults() {
			Results = Results ?? new List<ValidationResult>();
		}


		internal void RemoveEmptyResults() {
			Results?.RemoveAll(x => x.ErrorMessages.Count == 0);
		}
	}






	public struct Param<V> {

		internal Param
		(Validator v) { this.v = v; }



		private Validator v;
		public string Name { get => v._paramName; set => v._paramName = value; }
		public V Value { get => v._paramValue; set => v._paramValue = value; }
		public bool IsValid => v._paramIsValid;
		public ValidationResult Result => v._paramResult;



		public Param<V> UseEqualizer(
		ValueAdjuster<V> equalizer) { v._paramEqualizer = equalizer; return this; }



		public Param<V> UseEqualizer(
		StrOp eq) { v._paramEqualizer = eq; return this; }



		private dynamic Equalize(dynamic dynVal, dynamic dynEq) {
			if (dynVal == null || dynEq == null) return null;
			if (dynVal is string str && dynEq is StrOp op)
				dynVal = op.Apply(str);
			else if (dynVal is V val && dynEq is ValueAdjuster<V> adj)
				dynVal = adj(val);
			else if (dynVal is IEnumerable<string> strs && dynEq is StrOp op2)
				dynVal = strs.Select(o => op2.Apply(o));
			else if (dynVal is IEnumerable<V> vals && dynEq is ValueAdjuster<V> adj2)
				dynVal = vals.Select(o => adj2(o));
			return dynVal;
		}



		public Param<V> Check
		(string msg, ValidationTest<V> test) {
			try {
				var value = Equalize(v._paramValue, v._paramEqualizer);
				var success = test(value);
				if (!success) v.AddErrorMessage(msg);
				v.LastTestHasPassed = success;
			}
			catch (Exception ex) {
				v.AddErrorMessage("[Exception] " + ex.Message);
				v.LastTestHasPassed = false;
			}
			return this;
		}






		public Param<V> Check<R>(
		string msg, ValidationTest<V, R> test, R reference) {
			if (!v.LastTestHasPassed) return this;
			try {
				var value = Equalize(v._paramValue, v._paramEqualizer);
				reference = Equalize(reference, v._paramEqualizer);
				var success = test(value, reference);
				if (!success) v.AddErrorMessage(msg);
				v.LastTestHasPassed = success;
			}
			catch (Exception ex) {
				v.AddErrorMessage("[Exception] " + ex.Message);
				v.LastTestHasPassed = false;
			}
			return this;
		}






		public Param<V> Check(
		string invalidMsg, bool validCondition) {
			if (!v.LastTestHasPassed) return this;
			v.LastTestHasPassed = validCondition;
			if (!validCondition) v.AddErrorMessage(invalidMsg);
			return this;
		}

		public Param<V> Check(bool validCondition) => Check("Valor inválido", validCondition);





		public Param<V> ContinueIf(bool condition) {
			if (!condition) v.LastTestHasPassed = false;
			return this;
		}
	}






	[DebuggerDisplay("{DD(), nq")]
	public struct ValidationResult {
		public ValidationResult(string fieldName) { FieldName = fieldName; ErrorMessages = new List<string>(); }
		public readonly string FieldName;
		public readonly List<string> ErrorMessages;
		#if DEBUG
		public string DD() => $"{nameof(FieldName)}=\"{FieldName}\", {nameof(ErrorMessages)}.Count={ErrorMessages.Count}";
		#endif
	}






	internal static class Tests {
		public static bool NotNull<V>(V x) => x != null;
		public static bool NotEmpty<V>(IEnumerable<V> x) => x != null && x.Count() > 0;
		public static bool NotBlank(string x) => !string.IsNullOrWhiteSpace(x);
		public static bool IsMatch(string x, string a) => x != null && Regex.IsMatch(x, a);
		public static bool In<V>(V x, IEnumerable<V> a) => x != null && a.Contains(x);
		public static bool Equals<V>(V x, V a) where V : IEquatable<V> => x != null && x.Equals(a);
		public static bool MaxLength<V>(IEnumerable<V> x, int t) => x == null || x.Count() <= t;
		public static bool MinLength<V>(IEnumerable<V> x, int t) => x != null && x.Count() >= t;
		public static bool Length<V>(IEnumerable<V> x, int t) => x != null && x.Count() == t;
		public static bool Min<V>(V x, V t) where V : IComparable<V> => x != null && x.CompareTo(t) >= 0;
		public static bool Max<V>(V x, V t) where V : IComparable<V> => x == null || x.CompareTo(t) <= 0;
		public static bool Exactly<V>(V x, V t) where V : IComparable<V> => x != null && x.CompareTo(t) == 0;
		public static bool Min<V>(IEnumerable<V> x, int t) => x != null && x.Count() >= t;
		public static bool Max<V>(IEnumerable<V> x, int t) => x == null || x.Count() <= t;
		public static bool Exactly<V>(IEnumerable<V> x, int t) => x != null && x.Count() == t;
		public static bool IsEmail(string x) => Regex.IsMatch(x, @"^\w+([.-]?\w+)*@\w+([.-]?\w+)*(\.\w{2,3})+$");
		public static bool IsDigitsOnly(string x) => x != null && x.All(y => char.IsDigit(y));
	}






	public static class ParamTesterExtensions {
		// generic
		public static Param<V> NotNull<V>(this Param<V> field, string msg = null) {
			field.Check(msg ?? $"Não pode ser nulo", Tests.NotNull); return field;
		}
		public static Param<V> In<V>(this Param<V> field, IEnumerable<V> target, string msg = null) {
			field.Check(msg ?? $"Não é um valor válido", Tests.In, target); return field;
		}

		// IEquatable
		public static Param<V> Equals<V>(this Param<V> field, V allowed, string msg = null) where V : IEquatable<V> {
			field.Check(msg ?? $"Deve ser {allowed}", Tests.Equals, allowed); return field;
		}

		// IComparable
		public static Param<V> Min<V>(this Param<V> field, V allowed, string msg = null) where V : IComparable<V> {
			field.Check(msg ?? $"Não pode ser menor que {allowed}", Tests.Min, allowed); return field;
		}
		public static Param<V> Max<V>(this Param<V> field, V allowed, string msg = null) where V : IComparable<V> {
			field.Check(msg ?? $"Não pode ser maior que {allowed}", Tests.Max, allowed); return field;
		}
		public static Param<V> Exactly<V>(this Param<V> field, V reference, string msg = null) where V : IComparable<V> {
			field.Check(msg ?? $"Deve ser exatamente {reference}", Tests.Exactly, reference); return field;
		}

		// IEnumerable
		public static Param<IEnumerable<V>> NotEmpty<V>(this Param<IEnumerable<V>> field, string msg = null) {
			field.Check(msg ?? $"Não está preenchido", Tests.NotEmpty); return field;
		}
		public static Param<IEnumerable<V>> Length<V>(this Param<IEnumerable<V>> field, int length, string msg = null) {
			var name = typeof(V) == typeof(string) ? "caracteres" : "itens";
			field.Check(msg ?? $"Deve ter exatamente {length} itens", Tests.Length, length); return field;
		}
		public static Param<IEnumerable<V>> MinLength<V>(this Param<IEnumerable<V>> field, int allowed, string msg = null) {
			var name = typeof(V) == typeof(string) ? "caracteres" : "itens";
			field.Check(msg ?? $"Não pode ser menor que {allowed} {name}", Tests.MinLength, allowed); return field;
		}
		public static Param<IEnumerable<V>> MaxLength<V>(this Param<IEnumerable<V>> field, int allowed, string msg = null) {
			var name = typeof(V) == typeof(string) ? "caracteres" : "itens";
			field.Check(msg ?? $"Não pode ser maior que {allowed} itens", Tests.MaxLength, allowed); return field;
		}

		// int
		public static Param<int> Min(this Param<int> field, int number, string msg = null) {
			field.Check(msg ?? $"Não pode ser menor que {number}", Tests.Min, number); return field;
		}
		public static Param<int> Max(this Param<int> field, int number, string msg = null) {
			field.Check(msg ?? $"Não pode ser maior que {number}", Tests.Max, number); return field;
		}
		public static Param<int> Exactly(this Param<int> field, int number, string msg = null) {
			field.Check(msg ?? $"Deve ser exatamente {number}", Tests.Exactly, number); return field;
		}

		// string
		public static Param<string> NotEmpty(this Param<string> field, string msg = null) {
			field.Check(msg ?? $"Não está preenchido", Tests.NotEmpty); return field;
		}
		public static Param<string> NotBlank(this Param<string> field, string msg = null) {
			field.Check(msg ?? $"Não está preenchido", Tests.NotEmpty); return field;
		}
		public static Param<string> Length(this Param<string> field, int length, string msg = null) {
			field.Check(msg ?? $"Deve ter exatamente {length} caracteres", Tests.Length, length); return field;
		}
		public static Param<string> MinLength(this Param<string> field, int length, string msg = null) {
			field.Check(msg ?? $"Não pode ser menor que {length} caracteres", Tests.MinLength, length); return field;
		}
		public static Param<string> MaxLength(this Param<string> field, int length, string msg = null) {
			field.Check(msg ?? $"Não pode ser maior que {length} caracteres", Tests.MaxLength, length); return field;
		}
		public static Param<string> IsMatch(this Param<string> field, string pattern, string msg = null) {
			field.Check(msg ?? "Não é uma string válida", Tests.IsMatch, pattern); return field;
		}
		public static Param<string> IsEmail(this Param<string> field, string msg = null) {
			field.Check(msg ?? "Não é um e-mail válido", Tests.IsEmail); return field;
		}
		public static Param<string> IsDigitsOnly(this Param<string> field, string msg = null) {
			field.Check(msg ?? "Deve conter somente digitos (0-9)", Tests.IsDigitsOnly); return field;
		}
	}






	public static class StringExtensions {
		public static string Apply(this StrOp op, string x) {
			if (op.HasFlag(StrOp.Trim)) { x = x.Trim(); }
			if (op.HasFlag(StrOp.ToLower)) { x = x.ToUpper(); }
			if (op.HasFlag(StrOp.ToUpper)) { x = x.ToUpper(); }
			if (op.HasFlag(StrOp.RemoveDiacritics)) { x = x.RemoveDiacritics(); }
			if (op.HasFlag(StrOp.ToASCII)) { x = x.ToASCII(); }
			return x;
		}






		internal static IEnumerable<V> Apply<V>(this ValueAdjuster<V> f, IEnumerable<V> collection) => collection.Select(y => f(y));
		public static IEnumerable<string> Trim(this IEnumerable<string> texts) => texts.Select(u => u.Trim());
		public static IEnumerable<string> ToLower(this IEnumerable<string> texts) => texts.Select(u => u.ToLower());
		public static IEnumerable<string> ToUpper(this IEnumerable<string> texts) => texts.Select(u => u.ToUpper());
		public static IEnumerable<string> ToASCII(this IEnumerable<string> texts) => texts.Select(u => u.ToASCII());
		public static string ToASCII(this string text) => Regex.Replace(RemoveDiacritics(text), @"[^\u0000-\u007F]+", "*");
		public static IEnumerable<string> RemoveDiacritics(this IEnumerable<string> text) => text.Select(t => t.RemoveDiacritics());
		public static string RemoveDiacritics(this string text) {
			var normalizedString = text.Normalize(NormalizationForm.FormD);
			var sb = new StringBuilder(normalizedString.Length);
			for (int i = 0; i < normalizedString.Length; i++) {
				char c = normalizedString[i];
				var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
				if (unicodeCategory != UnicodeCategory.NonSpacingMark) sb.Append(c);
			}
			return sb.ToString().Normalize(NormalizationForm.FormC);
		}
	}






	public enum StrOp { Trim = 1, ToLower = 2, ToUpper = 4, RemoveDiacritics = 8, ToASCII = 16, /* OnlyWord = 16, OnlySentence = 32*/ }
	public delegate T ValueAdjuster<T>(T value);
	public delegate bool ValidationTest<V>(V value);
	public delegate bool ValidationTest<V, R>(V value, R allowed = default);
	public delegate void ValidationScript(Validator v);
}
