using System;
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
	///	Objeto para validação de qualquer outro objeto e seus membros.
	/// </summary>
	/// <changelog>
	/// Validator 0.2.0.3:
	/// - Validator e validation foram fundidos em um so objeto.
	/// - A classe Field foi absorvida pela Validator para aumentar a performace, pois assim não será necessário
	///   criar um novo objeto no heap a cada chamada da função Field;
	/// - Novo struct Tester ocupou lugar do antigo Field. Ele possui um único membro que é uma referência para
	///   o próprio Validator de forma a permitir que o struct seja retornado no diversas vezes durante a validação
	///   sem ocupar muito espaço do stack e aumentando a performance.
	/// - Novo ValidationText[V,C] permite que faça-se testes com parâmetros possam ser predefinidos em vez de ser delegates gerados em runtime
	/// </changelog>
	public class Validator {
		public Validator( dynamic obj ) {
			Object = obj;
		}




		public dynamic Object { get; private set; }
		public List<ValidationResult> Results { get; private set; }
		public string CurrentFieldName { get; internal set; }
		public dynamic CurrentFieldValue { get; internal set; }
		public bool CurrentFieldIsValid { get; internal set; }
		public ValidationResult CurrentFieldResult { get; internal set; }
		public string CurrentFieldLastResult => CurrentFieldResult.ErrorMessages.LastOrDefault();





		public void Reset( dynamic obj = null ) {
			Object = obj;
			Results = null;
			CurrentFieldName = null;
			CurrentFieldValue = null;
			CurrentFieldIsValid = false;
			CurrentFieldResult = new ValidationResult();
		}




		internal void InitializeResults() {
			Results = Results ?? new List<ValidationResult>();
		}




		public Tester<V> Field<V>( string name, V value ) {
			CurrentFieldName = name;
			CurrentFieldValue = value;
			CurrentFieldIsValid = true;
			CurrentFieldResult = new ValidationResult();
			return new Tester<V>(this);
		}




		internal void RemoveEmptyResults() {
			Results?.RemoveAll(x => x.ErrorMessages.Count == 0);
		}
	}







	public struct Tester<V> {
		internal Tester( Validator v ) { this.v=v; }


		private Validator v;


		public Tester<V> Test( string msg, ValidationTest<V> test ) {
			try {
				var success = test(v.CurrentFieldValue);
				if (!success) AddErrorMessage(msg);
			}
			catch (Exception ex) {
				AddErrorMessage("[Exception] " + ex.Message);
			}
			return this;
		}




		public Tester<V> Test<C>( string msg, ValidationTest<V, C> test, C targetValue ) {
			try {
				var success = test(v.CurrentFieldValue, targetValue);
				if (!success) AddErrorMessage(msg);
			}
			catch (Exception ex) {
				AddErrorMessage("[Exception] " + ex.Message);
			}
			return this;
		}




		public Tester<V> Test<C, M>( string msg, ValidationTest<V, C, M> test, C targetValue, M mode ) {
			try {
				var success = test(v.CurrentFieldValue, targetValue, mode);
				if (!success) AddErrorMessage(msg);
			}
			catch (Exception ex) {
				AddErrorMessage("[Exception] " + ex.Message);
			}
			return this;
		}




		public Tester<V> Test( string msg, bool success ) {
			if (!success) AddErrorMessage(msg);
			return this;
		}




		public Tester<V> Test( string msg, ValidationRule<V> rule ) {
			try {
				var success = rule.Test(v.CurrentFieldValue);
				if (!success) AddErrorMessage(rule.FailureMessage);
			}
			catch (Exception ex) {
				AddErrorMessage("[Exception] " + ex.Message);
			}
			return this;
		}




		public void AddErrorMessage( string msg ) {
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
		public ValidationResult( string fieldName ) { FieldName=fieldName; ErrorMessages = new List<string>(); }
		public readonly string FieldName;
		public readonly List<string> ErrorMessages;
#if DEBUG
		public string DD() => $"{nameof(FieldName)}=\"{FieldName}\", {nameof(ErrorMessages)}.Count={ErrorMessages.Count}";
#endif
	}








	public struct ValidationRule<V> {
		public ValidationRule( string failureMessage, ValidationTest<V> test ) {
			Test = test;
			FailureMessage = failureMessage;
		}
		public ValidationTest<V> Test;
		public string FailureMessage;
	}









	public delegate bool ValidationTest<V>( V value );
	public delegate bool ValidationTest<V, C>( V value, C value2 );
	public delegate bool ValidationTest<V, C, M>( V value, C value2, M mode );
	public delegate void ValidationScript( Validator v );






	internal static class Tests {
		public static bool NotNull( object x ) => x != null;
		public static bool NotEmpty<V>( IEnumerable<V> x ) => x.Count() > 0;
		public static bool NotBlank( string x ) => !string.IsNullOrWhiteSpace(x);
		public static bool IsMatch( string x, string a ) => Regex.IsMatch(x, a);
		public static bool In<V>( V x, IEnumerable<V> a ) => a.Contains(x);
		public static bool In<V>( V x, IEnumerable<V> a, Adjustment<V> f ) => f.Apply(a).Contains(f(x));
		public static bool In( string x, IEnumerable<string> a, StringOp f ) => f.Apply(a).Contains(f.Apply(x));
		public static bool Equals<V>( V x, V a ) where V : IEquatable<V> => x.Equals(a);
		public static bool Equals<V>( V x, V a, Adjustment<V> f) where V : IEquatable<V> => f(x).Equals(f(a));
		public static bool Equals( string x, string t, StringOp f ) => f.Apply(x).Equals(f.Apply(t));
		//public static bool MaxLength(string x, int t) => x.Length <= t;
		public static bool MaxLength<V>( IEnumerable<V> x, int t ) => x.Count() <= t;
		//public static bool MinLength(string x, int t) => x.Length >= t;
		public static bool MinLength<V>( IEnumerable<V> x, int t ) => x.Count() >= t;
		public static bool Max<V>( V x, V t ) where V : IComparable<V> => x.CompareTo(t) <= 0;
		public static bool Min<V>( V x, V t ) where V : IComparable<V> => x.CompareTo(t) >= 0;
	}






	public static class TesterExtensions {
		public static Tester<object> NotNull( this Tester<object> field, string msg = null ) {
			field.Test(msg??$"Não pode ser nulo", Tests.NotNull); return field;
		}
		public static Tester<IEnumerable<V>> NotEmpty<V>( this Tester<IEnumerable<V>> field, string msg = null ) {
			field.Test(msg??$"Não está preenchido", Tests.NotEmpty); return field;
		}
		public static Tester<string> NotBlank( this Tester<string> field, string msg = null ) {
			field.Test(msg??$"Não está preenchido", Tests.NotEmpty); return field;
		}
		public static Tester<string> IsMatch(this Tester<string> field, string pattern, string msg = null) {
			field.Test(msg??"Não é uma string válida", Tests.IsMatch, pattern); return field;
		}
		public static Tester<V> In<V>( this Tester<V> field, IEnumerable<V> target, string msg = null ) {
			field.Test(msg??$"Não é um valor válido", Tests.In, target); return field;
		}
		public static Tester<V> In<V>( this Tester<V> field, IEnumerable<V> allowed, Adjustment<V> adjustment, string msg = null ) {
			field.Test(msg??$"Não é um valor válido", Tests.In, allowed, adjustment); return field;
		}
		public static Tester<string> In( this Tester<string> field, IEnumerable<string> allowed, StringOp op, string msg = null ) {
			field.Test(msg??$"Não é um valor válido", Tests.In, allowed, op); return field;
		}
		public static Tester<V> Equals<V>( this Tester<V> field, V allowed, string msg = null ) where V : IEquatable<V> {
			field.Test(msg??$"Deve ser {allowed}", Tests.Equals, allowed); return field;
		}
		public static Tester<V> Equals<V>( this Tester<V> field, V allowed, Adjustment<V> adjustment, string msg = null ) where V : IEquatable<V> {
			field.Test(msg??$"Valor inválido", Tests.Equals, allowed, adjustment); return field;
		}
		public static Tester<string> Equals( this Tester<string> field, string allowed, StringOp op, string msg = null ) {
			field.Test(msg??$"Deve ser {allowed}", Tests.Equals, allowed, op); return field;
		}
		public static Tester<IEnumerable<V>> MaxLength<V>( this Tester<IEnumerable<V>> field, int allowed, string msg = null ) {
			field.Test(msg??$"Não pode ser maior que {allowed} caracteres", Tests.MaxLength, allowed); return field;
		}
		public static Tester<IEnumerable<V>> MinLength<V>( this Tester<IEnumerable<V>> field, int allowed, string msg = null ) {
			field.Test(msg??$"Não pode ser menor que {allowed} caracteres", Tests.MinLength, allowed); return field;
		}
		public static Tester<V> Max<V>( this Tester<V> field, V allowed, string msg = null ) where V : IComparable<V> {
			field.Test(msg??$"Não pode ser maior que {allowed}", Tests.Max, allowed); return field;
		}
		public static Tester<V> Min<V>( this Tester<V> field, V allowed, string msg = null ) where V : IComparable<V> {
			field.Test(msg??$"Não pode ser menor que {allowed}", Tests.Min, allowed); return field;
		}
	}






	public delegate T Adjustment<T>( T value );
	public enum StringOp { Trim=1, Upper=2, RemoveDiacritics=4, ToASCII = 8, /* OnlyWord = 16, OnlySentence = 32*/ }






	public static class StringExtensions {
		internal static string Apply(this StringOp op, string x) {
			if (op.HasFlag(StringOp.Trim)) { x = x.Trim(); }
			if (op.HasFlag(StringOp.Upper)) { x = x.ToUpper(); }
			if (op.HasFlag(StringOp.RemoveDiacritics)) { x = x.RemoveDiacritics(); }
			if (op.HasFlag(StringOp.ToASCII)) { x = x.ToASCII(); }
			return x;
			//if (m.HasFlag(Mode.OnlyWord)) { x = Regex.Replace(x,@"[^\w]-", ""); }
			//if (m.HasFlag(Mode.OnlySentence)) { x = Regex.Replace(x, @"[^\w-.\s]", ""); }
		}

		internal static IEnumerable<string> Apply(this StringOp op, IEnumerable<string> x) {
			if (op.HasFlag(StringOp.Trim)) { x = x.Trim(); }
			if (op.HasFlag(StringOp.Upper)) { x = x.ToUpper(); }
			if (op.HasFlag(StringOp.RemoveDiacritics)) { x = x.RemoveDiacritics(); }
			if (op.HasFlag(StringOp.ToASCII)) { x = x.ToASCII(); }
			return x;
		}

		internal static IEnumerable<V> Apply<V>( this Adjustment<V> f, IEnumerable<V> collection ) => collection.Select(y => f(y));
		//public static IEnumerable<string> Contains(this IEnumerable<string>, Mode mode)
		public static IEnumerable<string> Trim(this IEnumerable<string> texts) => texts.Select(u => u.Trim());
		public static IEnumerable<string> ToUpper(this IEnumerable<string> texts) => texts.Select(u => u.ToUpper());
		public static IEnumerable<string> ToASCII(this IEnumerable<string> texts) => texts.Select(u => u.ToASCII());
		public static string ToASCII(this string text) => Regex.Replace(RemoveDiacritics(text), @"[^\u0000-\u007F]+", "*");
		public static IEnumerable<string> RemoveDiacritics( this IEnumerable<string> text ) => text.Select(t => t.RemoveDiacritics());
		public static string RemoveDiacritics( this string text ) {
			var normalizedString = text.Normalize(NormalizationForm.FormD);
			var sb = new StringBuilder(normalizedString.Length);
			for (int i = 0; i<normalizedString.Length; i++) {
				char c = normalizedString[i];
				var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
				if (unicodeCategory != UnicodeCategory.NonSpacingMark) sb.Append(c);
			}
			return sb.ToString().Normalize(NormalizationForm.FormC);
		}

		//static string RegexReplace(this string text, string pattern, string replacement = "") => Regex.Replace(text, pattern, replacement);
	}
}
