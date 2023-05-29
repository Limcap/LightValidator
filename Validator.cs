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
		public static bool IsMatch( string x, string t ) => Regex.IsMatch(x, t);
		public static bool In( string x, IEnumerable<string> t, StringMode m ) => m.Apply(t).Contains(x);
		public static bool In<V>( V x, IEnumerable<V> t ) => t.Contains(x);
		public static bool Equals<V>( V x, V t ) where V : IEquatable<V> => x.Equals(t);
		public static bool Equals( string x, string t, StringMode m ) => m.Apply(x).Equals(m.Apply(t));
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
		public static Tester<string> In( this Tester<string> field, IEnumerable<string> target, StringMode mode, string msg = null ) {
			field.Test(msg??$"Não é um valor válido", Tests.In, target, mode); return field;
		}
		public static Tester<V> Equals<V>( this Tester<V> field, V target, string msg = null ) where V : IEquatable<V> {
			field.Test(msg??$"Deve ser {target}", Tests.Equals, target); return field;
		}
		public static Tester<string> Equals( this Tester<string> field, string target, StringMode mode, string msg = null ) {
			field.Test(msg??$"Deve ser {target}", Tests.Equals, target, mode); return field;
		}
		public static Tester<IEnumerable<V>> MaxLength<V>( this Tester<IEnumerable<V>> field, int target, string msg = null ) {
			field.Test(msg??$"Não pode ser maior que {target} caracteres", Tests.MaxLength, target); return field;
		}
		public static Tester<IEnumerable<V>> MinLength<V>( this Tester<IEnumerable<V>> field, int target, string msg = null ) {
			field.Test(msg??$"Não pode ser menor que {target} caracteres", Tests.MinLength, target); return field;
		}
		public static Tester<V> Max<V>( this Tester<V> field, V target, string msg = null ) where V : IComparable<V> {
			field.Test(msg??$"Não pode ser maior que {target}", Tests.Max, target); return field;
		}
		public static Tester<V> Min<V>( this Tester<V> field, V target, string msg = null ) where V : IComparable<V> {
			field.Test(msg??$"Não pode ser menor que {target}", Tests.Min, target); return field;
		}
	}






	public enum StringMode { IgnoreWhitespace=1, IgnoreCase=2, IgnoreDiacritics=4, /*OnlyASCII = 8, OnlyWord = 16, OnlySentence = 32*/ }






	public static class StringExtensions {
		public static bool HasFlag(this StringMode mode, StringMode checkflag) => (mode & checkflag)==checkflag;

		public static string Apply(this StringMode m, string x) {
			if (m.HasFlag(StringMode.IgnoreWhitespace)) { x = x.Trim(); }
			if (m.HasFlag(StringMode.IgnoreCase)) { x = x.ToUpper(); }
			if (m.HasFlag(StringMode.IgnoreDiacritics)) { x = x.RemoveDiacritics(); }
			return x;
			//if (m.HasFlag(Mode.OnlyASCII)) { x = x.ToASCII(); }
			//if (m.HasFlag(Mode.OnlyWord)) { x = Regex.Replace(x,@"[^\w]-", ""); }
			//if (m.HasFlag(Mode.OnlySentence)) { x = Regex.Replace(x, @"[^\w-.\s]", ""); }
		}

		public static IEnumerable<string> Apply(this StringMode m, IEnumerable<string> x) {
			if (m.HasFlag(StringMode.IgnoreWhitespace)) { x = x.Trim(); }
			if (m.HasFlag(StringMode.IgnoreCase)) { x = x.ToUpper(); }
			if (m.HasFlag(StringMode.IgnoreDiacritics)) { x = x.RemoveDiacritics(); }
			return x;
		}

		//public static IEnumerable<string> Contains(this IEnumerable<string>, Mode mode)
		public static IEnumerable<string> Trim(this IEnumerable<string> t) => t.Select(u => u.Trim());
		public static IEnumerable<string> ToUpper(this IEnumerable<string> t) => t.Select(u => u.ToUpper());
		public static IEnumerable<string> RemoveDiacritics(this IEnumerable<string> text) => text.Select(t => t.RemoveDiacritics());

		public static string RemoveDiacritics( this string text ) {
			var normalizedString = text.Normalize(System.Text.NormalizationForm.FormD);
			var sb = new StringBuilder(normalizedString.Length);
			for (int i = 0; i<normalizedString.Length; i++) {
				char c = normalizedString[i];
				var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
				if (unicodeCategory != UnicodeCategory.NonSpacingMark) sb.Append(c);
			}
			return sb.ToString().Normalize(NormalizationForm.FormC);
		}

		public static IEnumerable<string> ASCII(this IEnumerable<string> t) => t.Select(u => u.ToASCII());

		public static string ToASCII(this string text) => Regex.Replace(RemoveDiacritics(text), @"[^\u0000-\u007F]+", "*");

		static string RegexReplace(this string text, string pattern, string replacement = "") => Regex.Replace(text, pattern, replacement);
	}
}
