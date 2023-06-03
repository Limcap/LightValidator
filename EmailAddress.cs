using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Limcap.LightValidator {
	public struct EmailAddress {
		public EmailAddress(string value) { Value = value; }
		public string Value;
		public bool IsValid { get => Regex.IsMatch(Value, @"^\w+([.-]?\w+)*@\w+([.-]?\w+)*(\.{2,3})+$"); }
		public string UserName => !IsValid ? null : Value.Substring(0, Value.IndexOf('@'));
		public string Provider => !IsValid ? null : Value.Substring('@');
		public override string ToString() => Value;
	}
}
