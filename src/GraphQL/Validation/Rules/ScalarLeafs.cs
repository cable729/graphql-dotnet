﻿using System;
using GraphQL.Language;
using GraphQL.Types;
using System.Linq;

namespace GraphQL.Validation.Rules
{
    /// <summary>
    /// Scalar leafs
    ///
    /// A GraphQL document is valid only if all leaf fields (fields without
    /// sub selections) are of scalar or enum types.
    /// </summary>
    public class ScalarLeafs : IValidationRule
    {
        public Func<string, string, string> NoSubselectionAllowedMessage = (field, type) =>
            $"Field {field} of type {type} must not have a sub selection";

        public Func<string, string, string> RequiredSubselectionMessage = (field, type) =>
            $"Field {field} of type {type} must have a sub selection";

        public INodeVisitor Validate(ValidationContext context)
        {
            return new NodeVisitorMatchFuncListener<Field>(
                n => n is Field,
                f => Field(context.TypeInfo.GetLastType(), f, context));
        }

        private void Field(GraphType type, Field field, ValidationContext context)
        {
            if (type == null)
            {
                return;
            }

            if (type.IsLeafType(context.Schema))
            {
                if (field.SelectionSet != null && field.SelectionSet.Selections.Any())
                {
                    var error = new ValidationError("", NoSubselectionAllowedMessage(field.Name, type.Name), field);
                    error.AddLocation(field.SourceLocation.Line, field.SourceLocation.Column);
                    context.ReportError(error);
                }
            }
            else if(field.SelectionSet == null || !field.SelectionSet.Selections.Any())
            {
                var error = new ValidationError("", RequiredSubselectionMessage(field.Name, type.Name), field);
                error.AddLocation(field.SourceLocation.Line, field.SourceLocation.Column);
                context.ReportError(error);
            }
        }
    }
}
