using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Schema;

namespace Limcap.LightValidator {

	/// <summary>
	///	Provides validation for any object and its members.
	/// </summary>
	public class Validator {
		public Validator(dynamic obj) {
			Object = obj;
		}




		public dynamic Object { get; private set; }
		public List<ValidationResult> Results { get; private set; }
		public string CurrentFieldName { get; internal set; }
		public dynamic CurrentFieldValue { get; internal set; }
		public dynamic CurrentEqualizer { get; internal set; }
		public bool CurrentFieldIsValid { get; internal set; }
		public ValidationResult CurrentFieldResult { get; internal set; }
		public string CurrentFieldLastResult => CurrentFieldResult.ErrorMessages.LastOrDefault();




		public void Reset(dynamic obj = null) {
			Object = obj;
			Results = null;
			CurrentFieldName = null;
			CurrentFieldValue = null;
			CurrentFieldIsValid = false;
			CurrentFieldResult = new ValidationResult();
			CurrentEqualizer = null;
		}




		internal void InitializeResults() {
			Results = Results ?? new List<ValidationResult>();
		}




		public Tester<V> Field<V>(string name, V value) {
			CurrentFieldName = name;
			CurrentFieldValue = value;
			CurrentFieldIsValid = true;
			CurrentFieldResult = new ValidationResult();
			CurrentEqualizer = null;
			return new Tester<V>(this);
		}




		internal void RemoveEmptyResults() {
			Results?.RemoveAll(x => x.ErrorMessages.Count == 0);
		}
	}







	public struct Tester<V> {

		internal Tester
		(Validator v) { this.v=v; }


		private Validator v;


		public Tester<V> Equalizer
		(ValueAdjuster<V> equalizer) { v.CurrentEqualizer = equalizer; return this; }


		public Tester<V> Equalizer
		(StrOp eq) { v.CurrentEqualizer = eq; return this; }






		private dynamic Equalize(dynamic dynVal, dynamic dynEq) {
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






		public Tester<V> Test
		(string msg, ValidationTest<V> test) {
			try {
				var value = Equalize(v.CurrentFieldValue, v.CurrentEqualizer);
				var success = test(value);
				if (!success) AddErrorMessage(msg);
			}
			catch (Exception ex) {
				AddErrorMessage("[Exception] " + ex.Message);
			}
			return this;
		}






		public Tester<V> Test<R>
		(string msg, ValidationTest<V, R> test, R reference) {
			try {
				var value = Equalize(v.CurrentFieldValue, v.CurrentEqualizer);
				reference = Equalize(reference, v.CurrentEqualizer); 
				var success = test(value, reference);
				if (!success) AddErrorMessage(msg);
			}
			catch (Exception ex) {
				AddErrorMessage("[Exception] " + ex.Message);
			}
			return this;
		}






		public Tester<V> Test(string msg, bool success) {
			if (!success) AddErrorMessage(msg);
			return this;
		}






		public void AddErrorMessage(string msg) {
			if (v.CurrentFieldIsValid) {
				v.CurrentFieldResult = new ValidationResult(v.CurrentFieldName);
				v.InitializeResults();
				v.Results.Add(v.CurrentFieldResult);
				v.CurrentFieldIsValid = false;
			}
			v.CurrentFieldResult.ErrorMessages.Add(msg);
		}
	}






	[DebuggerDisplay("{DD(), nq")]
	public struct ValidationResult {
		public ValidationResult(string fieldName) { FieldName=fieldName; ErrorMessages = new List<string>(); }
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
		public static Tester<object> NotNull(this Tester<object> field, string msg = null) {
			field.Test(msg??$"Não pode ser nulo", Tests.NotNull); return field;
		}
		public static Tester<IEnumerable<V>> NotEmpty<V>(this Tester<IEnumerable<V>> field, string msg = null) {
			field.Test(msg??$"Não está preenchido", Tests.NotEmpty); return field;
		}
		public static Tester<string> NotBlank(this Tester<string> field, string msg = null) {
			field.Test(msg??$"Não está preenchido", Tests.NotEmpty); return field;
		}
		public static Tester<string> IsMatch(this Tester<string> field, string pattern, string msg = null) {
			field.Test(msg??"Não é uma string válida", Tests.IsMatch, pattern); return field;
		}
		public static Tester<V> In<V>(this Tester<V> field, IEnumerable<V> target, string msg = null) {
			field.Test(msg??$"Não é um valor válido", Tests.In, target); return field;
		}
		public static Tester<V> Equals<V>(this Tester<V> field, V allowed, string msg = null) where V : IEquatable<V> {
			field.Test(msg??$"Deve ser {allowed}", Tests.Equals, allowed); return field;
		}
		public static Tester<IEnumerable<V>> MaxLength<V>(this Tester<IEnumerable<V>> field, int allowed, string msg = null) {
			var name = typeof(V) == typeof(string) ? "caracteres" : "itens";
			field.Test(msg??$"Não pode ser maior que {allowed} itens", Tests.MaxLength, allowed); return field;
		}
		public static Tester<IEnumerable<V>> MinLength<V>(this Tester<IEnumerable<V>> field, int allowed, string msg = null) {
			var name = typeof(V) == typeof(string) ? "caracteres" : "itens";
			field.Test(msg??$"Não pode ser menor que {allowed} {name}", Tests.MinLength, allowed); return field;
		}
		public static Tester<V> Max<V>(this Tester<V> field, V allowed, string msg = null) where V : IComparable<V> {
			field.Test(msg??$"Não pode ser maior que {allowed}", Tests.Max, allowed); return field;
		}
		public static Tester<V> Min<V>(this Tester<V> field, V allowed, string msg = null) where V : IComparable<V> {
			field.Test(msg??$"Não pode ser menor que {allowed}", Tests.Min, allowed); return field;
		}
		public static Tester<string> IsEmail(this Tester<string> field, string msg = null) {
			field.Test(msg??"Não é um e-mail válido", Tests.IsEmail); return field;
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
			for (int i = 0; i<normalizedString.Length; i++) {
				char c = normalizedString[i];
				var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
				if (unicodeCategory != UnicodeCategory.NonSpacingMark) sb.Append(c);
			}
			return sb.ToString().Normalize(NormalizationForm.FormC);
		}
	}






	public enum StrOp { Trim=1, Upper=2, RemoveDiacritics=4, ToASCII = 8, /* OnlyWord = 16, OnlySentence = 32*/ }
	public delegate T ValueAdjuster<T>(T value);
	public delegate bool ValidationTest<V>(V value);
	public delegate bool ValidationTest<V, R>(V value, R allowed = default);
	public delegate void ValidationScript(Validator v);
}
