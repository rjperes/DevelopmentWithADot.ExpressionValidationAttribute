using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.ComponentModel;
using System.Management;

namespace DevelopmentWithADot.ExpressionValidationAttribute
{
	[Serializable]
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = true)]
	public sealed class ExpressionValidationAttribute : ValidationAttribute
	{
		public const String IsNotNull = "{0} <> NULL";
		public const String IsNull = "{0} = NULL";
		public const String IsPositive = "{0} > 0";
		public const String IsNegative = "{0} < 0";
		public const String IsZero = "{0} = 0";
		public const String IsPositiveOrZero = "{0} >= 0";
		public const String IsNegativeOrZero = "{0} <= 0";
		public const String IsOdd = "({0} % 2) <> 0";
		public const String IsEven = "({0} % 2) = 0";

		public ExpressionValidationAttribute(String expression)
		{
			this.Expression = expression;
		}

		/// <summary>
		/// The expression to evaluate. May not be null.
		/// Supported values:
		/// - PropertyName
		/// - null
		/// - {0}
		/// Supported operators:
		/// - &gt;
		/// - &lt;
		/// - &gte;
		/// - &lte;
		/// - ==
		/// - !=
		/// - %
		/// - (, )
		/// </summary>
		/// <example>
		/// PropertyA != null
		/// PropertyA > PropertyB
		/// </example>
		public String Expression
		{
			get;
			private set;
		}

		public override Boolean IsDefaultAttribute()
		{
			return (this.Expression == null);
		}

		public override Boolean Equals(Object obj)
		{
			if (base.Equals(obj) == false)
			{
				return (false);
			}

			if (Object.ReferenceEquals(this, obj) == true)
			{
				return (true);
			}

			ExpressionValidationAttribute other = obj as ExpressionValidationAttribute;

			if (other == null)
			{
				return (false);
			}

			return (other.Expression == this.Expression);
		}

		public override Int32 GetHashCode()
		{
			Int32 hashCode = 397 ^ (this.Expression != null ? this.Expression.GetHashCode() : 0);

			return (hashCode);
		}

		private static String Replace(String originalString, String oldValue, String newValue, StringComparison comparisonType)
		{
			Int32 startIndex = 0;

			while (true)
			{
				startIndex = originalString.IndexOf(oldValue, startIndex, comparisonType);
				
				if (startIndex < 0)
				{
					break;
				}

				originalString = String.Concat(originalString.Substring(0, startIndex), newValue, originalString.Substring(startIndex + oldValue.Length));

				startIndex += newValue.Length;
			}

			return (originalString);
		}
		
		protected override ValidationResult IsValid(Object value, ValidationContext validationContext)
		{
			if (String.IsNullOrWhiteSpace(this.Expression) == true)
			{
				return (ValidationResult.Success);
			}

			Object instance = validationContext.ObjectInstance;
			DataTable temp = new DataTable();
			String expression = this.Expression;

			while (expression.IndexOf("  ") >= 0)
			{
				expression = expression.Replace("  ", " ");
			}

			//translate .NET language operators into SQL ones
			expression = expression.Replace("!=", "<>");
			expression = expression.Replace("==", "=");
			expression = expression.Replace("!", " NOT ");
			expression = expression.Replace("&&", " AND ");
			expression = expression.Replace("||", " OR ");
			expression = Replace(expression, "= NULL", " IS NULL ", StringComparison.OrdinalIgnoreCase);
			expression = Replace(expression, "<> NULL", " IS NOT NULL ", StringComparison.OrdinalIgnoreCase);
			expression = Replace(expression, "null", "NULL", StringComparison.OrdinalIgnoreCase);
			expression = expression.Replace("{0}", validationContext.MemberName);

			PropertyDescriptor[] props = TypeDescriptor
				.GetProperties(instance)
				.OfType<PropertyDescriptor>()
				.Where(x => x.IsReadOnly == false)
				.Where(x => x.PropertyType.IsPrimitive || x.PropertyType == typeof(String))
				.ToArray();

			foreach (PropertyDescriptor prop in props)
			{
				temp.Columns.Add(prop.Name, prop.PropertyType);
			}

			temp.BeginLoadData();

			DataRow row = temp.NewRow();

			temp.Rows.Add(row);

			foreach (PropertyDescriptor prop in props)
			{
				row[prop.Name] = prop.GetValue(instance);
			}

			DataColumn isValidColumn = new DataColumn();
			isValidColumn.ColumnName = "_is_valid";
			isValidColumn.Expression = expression;

			temp.Columns.Add(isValidColumn);

			temp.EndLoadData();

			Boolean isValid = Convert.ToBoolean(row[isValidColumn]);

			if (isValid == true)
			{
				return (ValidationResult.Success);
			}
			else
			{
				String errorMessage = this.FormatErrorMessage(validationContext.MemberName != null ? validationContext.MemberName : validationContext.ObjectInstance.GetType().Name);
				return (new ValidationResult(errorMessage, ((validationContext.MemberName != null) ? new String[] { validationContext.MemberName } : Enumerable.Empty<String>())));
			}
		}
	}
}