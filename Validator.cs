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




		public dynamic Object { get; private set; }
		public List<ValidationResult> Results { get; private set; }
		public string CurrentFieldName { get; internal set; }
		public dynamic CurrentFieldValue { get; internal set; }
		public dynamic CurrentEqualizer { get; internal set; }
		public bool CurrentFieldIsValid { get; internal set; }
		public ValidationResult CurrentFieldResult { get; internal set; }
		public string LastError => Results.LastOrDefault().ErrorMessages?.LastOrDefault();
		public bool LastTestHasPassed { get; internal set; }




		public void Reset(dynamic obj = null) {
			Object = obj;
			Results = null;
			CurrentFieldName = null;
			CurrentFieldValue = null;
			CurrentEqualizer = null;
			CurrentFieldIsValid = false;
			CurrentFieldResult = new ValidationResult();
			LastTestHasPassed = false;
		}






		public ParamTester<V> Param<V>(string paramNmae, V paramValue) {
			CurrentFieldName = paramNmae;
			CurrentFieldValue = paramValue;
			CurrentFieldIsValid = true;
			CurrentFieldResult = new ValidationResult();
			CurrentEqualizer = null;
			LastTestHasPassed = true;
			return new ParamTester<V>(this);
		}






		public ParamTester Param(string paramName) {
			CurrentFieldName = paramName;
			CurrentFieldValue = null;
			CurrentEqualizer = null;
			CurrentFieldIsValid = true;
			CurrentFieldResult = new ValidationResult();
			LastTestHasPassed = true;
			return new ParamTester(this);
		}






		internal void AddErrorMessage(
		string msg) {
			if (CurrentFieldIsValid) {
				CurrentFieldResult = new ValidationResult(CurrentFieldName);
				InitializeResults();
				Results.Add(CurrentFieldResult);
				CurrentFieldIsValid = false;
			}
			CurrentFieldResult.ErrorMessages.Add(msg);
		}






		internal void InitializeResults() {
			Results = Results ?? new List<ValidationResult>();
		}


		internal void RemoveEmptyResults() {
			Results?.RemoveAll(x => x.ErrorMessages.Count == 0);
		}
	}







	public struct ParamTester {

		internal ParamTester
		(Validator v) { this.v = v; }


		Validator v;


		public ParamTester Check(
		string invalidMsg, bool validationCondition) {
			if (!v.LastTestHasPassed) return this;
			v.LastTestHasPassed = validationCondition;
			if (!validationCondition) v.AddErrorMessage(invalidMsg);
			return this;
		}
		public ParamTester Check(bool validationCondition) => Check("Valor inválido", validationCondition);




		public ParamTester ContinueIf(bool condition) {
			if (!condition) v.LastTestHasPassed = false;
			return this;
	}
	}






	public struct ParamTester<V> {

		internal ParamTester
		(Validator v) { this.v = v; }


		private Validator v;


		public ParamTester<V> UseEqualizer(
		ValueAdjuster<V> equalizer) { v.CurrentEqualizer = equalizer; return this; }


		public ParamTester<V> UseEqualizer(
		StrOp eq) { v.CurrentEqualizer = eq; return this; }






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






		public ParamTester<V> Check(
		string msg, ValidationTest<V> test) {
			try {
				var value = Equalize(v.CurrentFieldValue, v.CurrentEqualizer);
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






		public ParamTester<V> Check<R>(
		string msg, ValidationTest<V, R> test, R reference) {
			if (!v.LastTestHasPassed) return this;
			try {
				var value = Equalize(v.CurrentFieldValue, v.CurrentEqualizer);
				reference = Equalize(reference, v.CurrentEqualizer); 
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






		public ParamTester<V> Check(
		string invalidMsg, bool validCondition) {
			if (!v.LastTestHasPassed) return this;
			v.LastTestHasPassed = validCondition;
			if (!validCondition) v.AddErrorMessage(invalidMsg);
			return this;
		}

		public ParamTester<V> Check(bool validCondition) => Check("Valor inválido", validCondition);





		public ParamTester<V> ContinueIf(bool condition) {
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
		public static bool NotNull(object x) => x != null;
		public static bool NotEmpty<V>(IEnumerable<V> x) => x.Count() > 0;
		public static bool NotBlank(string x) => !string.IsNullOrWhiteSpace(x);
		public static bool IsMatch(string x, string a) => Regex.IsMatch(x, a);
		public static bool In<V>(V x, IEnumerable<V> a) => a.Contains(x);
		public static bool Equals<V>(V x, V a) where V : IEquatable<V> => x.Equals(a);
		public static bool MaxLength<V>(IEnumerable<V> x, int t) => x.Count() <= t;
		public static bool MinLength<V>(IEnumerable<V> x, int t) => x.Count() >= t;
		public static bool Max<V>(V x, V t) where V : IComparable<V> => x.CompareTo(t) <= 0;
		public static bool Min<V>(V x, V t) where V : IComparable<V> => x.CompareTo(t) >= 0;
		public static bool IsEmail(string x) => Regex.IsMatch(x, @"^\w+([.-]?\w+)*@\w+([.-]?\w+)*(\.\w{2,3})+$");
	}






	public static class TesterExtensions {
		public static ParamTester<object> NotNull(this ParamTester<object> field, string msg = null) {
			field.Check(msg??$"Não pode ser nulo", Tests.NotNull); return field;
		}
		public static ParamTester<IEnumerable<V>> NotEmpty<V>(this ParamTester<IEnumerable<V>> field, string msg = null) {
			field.Check(msg??$"Não está preenchido", Tests.NotEmpty); return field;
		}
		public static ParamTester<string> NotBlank(this ParamTester<string> field, string msg = null) {
			field.Check(msg??$"Não está preenchido", Tests.NotEmpty); return field;
		}
		public static ParamTester<string> IsMatch(this ParamTester<string> field, string pattern, string msg = null) {
			field.Check(msg??"Não é uma string válida", Tests.IsMatch, pattern); return field;
		}
		public static ParamTester<V> In<V>(this ParamTester<V> field, IEnumerable<V> target, string msg = null) {
			field.Check(msg??$"Não é um valor válido", Tests.In, target); return field;
		}
		public static ParamTester<V> Equals<V>(this ParamTester<V> field, V allowed, string msg = null) where V : IEquatable<V> {
			field.Check(msg??$"Deve ser {allowed}", Tests.Equals, allowed); return field;
		}
		public static ParamTester<IEnumerable<V>> MaxLength<V>(this ParamTester<IEnumerable<V>> field, int allowed, string msg = null) {
			var name = typeof(V) == typeof(string) ? "caracteres" : "itens";
			field.Check(msg??$"Não pode ser maior que {allowed} itens", Tests.MaxLength, allowed); return field;
		}
		public static ParamTester<IEnumerable<V>> MinLength<V>(this ParamTester<IEnumerable<V>> field, int allowed, string msg = null) {
			var name = typeof(V) == typeof(string) ? "caracteres" : "itens";
			field.Check(msg??$"Não pode ser menor que {allowed} {name}", Tests.MinLength, allowed); return field;
		}
		public static ParamTester<V> Max<V>(this ParamTester<V> field, V allowed, string msg = null) where V : IComparable<V> {
			field.Check(msg??$"Não pode ser maior que {allowed}", Tests.Max, allowed); return field;
		}
		public static ParamTester<V> Min<V>(this ParamTester<V> field, V allowed, string msg = null) where V : IComparable<V> {
			field.Check(msg??$"Não pode ser menor que {allowed}", Tests.Min, allowed); return field;
		}
		public static ParamTester<string> IsEmail(this ParamTester<string> field, string msg = null) {
			field.Check(msg??"Não é um e-mail válido", Tests.IsEmail); return field;
		}
	}






	public static class StringExtensions {
		internal static string Apply(this StrOp op, string x) {
			if (op.HasFlag(StrOp.Trim)) { x = x.Trim(); }
			if (op.HasFlag(StrOp.Upper)) { x = x.ToUpper(); }
			if (op.HasFlag(StrOp.RemoveDiacritics)) { x = x.RemoveDiacritics(); }
			if (op.HasFlag(StrOp.ToASCII)) { x = x.ToASCII(); }
			return x;
		}






		internal static IEnumerable<V> Apply<V>(this ValueAdjuster<V> f, IEnumerable<V> collection) => collection.Select(y => f(y));
		public static IEnumerable<string> Trim(this IEnumerable<string> texts) => texts.Select(u => u.Trim());
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
