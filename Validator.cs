using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
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
		internal Tester(Validator v) { this.v=v; }

		
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




		public Tester<V> Test<C>( string msg, ValidationTest<V,C> test, C targetValue ) {
			try {
				var success = test(v.CurrentFieldValue, targetValue);
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








	public static class TesterExtensions {
		public static class Tests {
			public static readonly ValidationTest<object> NotNull = x => x != null;
			public static readonly ValidationTest<string> NotEmpty = x => !string.IsNullOrWhiteSpace(x);
			public static readonly ValidationTest<string, int> MaxLength = (x,t) => x.Length <= t;
			public static readonly ValidationTest<string, int> MinLength = (x,t) => x.Length >= t;
			public static readonly ValidationTest<decimal, decimal> Max = (x,t) => x <= t;
			public static readonly ValidationTest<decimal, decimal> Min = (x,t) => x >= t;
			public static readonly ValidationTest<decimal, decimal> Exact = (x,t) => x == t;
		}

		public static Tester<object> NotNull( this Tester<object> field, string msg = null ) {
			field.Test(msg??$"Não pode ser nulo", Tests.NotNull); return field;
		}
		public static Tester<string> NotEmpty( this Tester<string> field, string msg = null ) {
			field.Test(msg??$"Não está preenchido", Tests.NotEmpty); return field;
		}
		public static Tester<string> MaxLength( this Tester<string> field, int target, string msg = null ) {
			field.Test(msg??$"Não pode ser maior que {target} caracteres", Tests.MaxLength, target); return field;
		}
		public static Tester<string> MinLength( this Tester<string> field, int target, string msg = null ) {
			field.Test(msg??$"Não pode ser menor que {target} caracteres", Tests.MinLength, target); return field;
		}
		public static Tester<decimal> Max( this Tester<decimal> field, decimal target, string msg = null ) {
			field.Test(msg??$"Não pode ser maior que {target}", Tests.Max, target); return field;
		}
		public static Tester<decimal> Min( this Tester<decimal> field, decimal target, string msg = null ) {
			field.Test(msg??$"Não pode ser menor que {target}", Tests.Min, target); return field;
		}
		public static Tester<decimal> Exact( this Tester<decimal> field, decimal target, string msg = null ) {
			field.Test(msg??$"Deve ser {target}", Tests.Exact, target); return field;
		}
	}








	public delegate bool ValidationTest<V>( V value );
	public delegate bool ValidationTest<V,C>( V value, C value2 );
	//public delegate string ValidationNamer( string originalName, dynamic validationSubject );
	public delegate void ValidationScript( Validator v );
}
