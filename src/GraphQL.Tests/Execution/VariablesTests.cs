﻿using System;
using System.Linq;
using GraphQL.Language;
using GraphQL.Types;
using GraphQL.Validation;
using Newtonsoft.Json;
using Should;

namespace GraphQL.Tests.Execution
{
    public class TestComplexScalarType : ScalarGraphType
    {
        public TestComplexScalarType()
        {
            Name = "ComplexScalar";
        }

        public override object Serialize(object value)
        {
            if (value is string && value.Equals("DeserializedValue"))
            {
                return "SerializedValue";
            }

            return value;
        }

        public override object ParseValue(object value)
        {
            if (value is string && value.Equals("SerializedValue"))
            {
                return "DeserializedValue";
            }

            return null;
        }

        public override object ParseLiteral(IValue value)
        {
            if (value is StringValue)
            {
                if (((StringValue) value).Value.Equals("SerializedValue"))
                {
                    return "DeserializedValue";
                }
            }

            return null;
        }
    }

    public class TestInputObject : InputObjectGraphType
    {
        public TestInputObject()
        {
            Name = "TestInputObject";
            Field<StringGraphType>("a");
            Field<ListGraphType<StringGraphType>>("b");
            Field<NonNullGraphType<StringGraphType>>("c");
            Field<TestComplexScalarType>("d");
        }
    }

    public class TestType : ObjectGraphType
    {
        public TestType()
        {
            Name = "TestType";

            Field<StringGraphType>(
                "fieldWithObjectInput",
                arguments: new QueryArguments(new []
                {
                    new QueryArgument<TestInputObject> { Name = "input"}
                }),
                resolve: context =>
                {
                    var result = JsonConvert.SerializeObject(context.Argument<object>("input"));
                    return result;
                });

            Field<StringGraphType>(
                "fieldWithNullableStringInput",
                arguments: new QueryArguments(new []
                {
                    new QueryArgument<StringGraphType> { Name = "input" }
                }),
                resolve: context =>
                {
                    var result = JsonConvert.SerializeObject(context.Argument<object>("input"));
                    return result;
                });

            Field<StringGraphType>(
                "fieldWithNonNullableStringInput",
                arguments: new QueryArguments(new []
                {
                    new QueryArgument<NonNullGraphType<StringGraphType>> { Name = "input" }
                }),
                resolve: context =>
                {
                    var result = JsonConvert.SerializeObject(context.Argument<object>("input"));
                    return result;
                });

            Field<StringGraphType>(
                "fieldWithDefaultArgumentValue",
                arguments: new QueryArguments(new []
                {
                    new QueryArgument<StringGraphType> { Name = "input", DefaultValue = "Hello World"}
                }),
                resolve: context =>
                {
                    var result = JsonConvert.SerializeObject(context.Argument<object>("input"));
                    return result;
                });
        }
    }

    public class VariablesSchema : Schema
    {
        public VariablesSchema()
        {
            Query = new TestType();
        }
    }

    public class Variables_With_Inline_Structs_Tests : QueryTestBase<VariablesSchema>
    {
        [Test]
        public void executes_with_complex_input()
        {
            var query = @"
            {
              fieldWithObjectInput(input: {a: ""foo"", b: [""bar""], c: ""baz""})
            }
            ";
            var expected = "{ \"fieldWithObjectInput\": \"{\\\"a\\\":\\\"foo\\\",\\\"b\\\":[\\\"bar\\\"],\\\"c\\\":\\\"baz\\\"}\" }";

            AssertQuerySuccess(query, expected);
        }

        [Test]
        public void properly_parses_single_value_to_list()
        {
            var query = @"
            {
              fieldWithObjectInput(input: {a: ""foo"", b: ""bar"", c: ""baz""})
            }
            ";
            var expected = "{ \"fieldWithObjectInput\": \"{\\\"a\\\":\\\"foo\\\",\\\"b\\\":[\\\"bar\\\"],\\\"c\\\":\\\"baz\\\"}\" }";

            AssertQuerySuccess(query, expected);
        }

        [Test]
        public void does_not_use_incorrect_value()
        {
            var query = @"
            {
              fieldWithObjectInput(input: [""foo"", ""bar"", ""baz""])
            }
            ";
            var expected = "{ \"fieldWithObjectInput\": \"null\" }";

            AssertQuerySuccess(query, expected, rules: Enumerable.Empty<IValidationRule>());
        }
    }

    public class UsingVariablesTests : QueryTestBase<VariablesSchema>
    {
        private string _query = @"
            query q($input: TestInputObject) {
              fieldWithObjectInput(input: $input)
            }
        ";

        [Test]
        public void executes_with_complex_input()
        {
            var expected = "{ \"fieldWithObjectInput\": \"{\\\"a\\\":\\\"foo\\\",\\\"b\\\":[\\\"bar\\\"],\\\"c\\\":\\\"baz\\\"}\" }";

            var inputs = "{'input': {'a':'foo', 'b':['bar'], 'c': 'baz'} }".ToInputs();

            AssertQuerySuccess(_query, expected, inputs);
        }

        [Test]
        public void properly_parses_single_value_to_list()
        {
            var expected = "{ \"fieldWithObjectInput\": \"{\\\"a\\\":\\\"foo\\\",\\\"b\\\":[\\\"bar\\\"],\\\"c\\\":\\\"baz\\\"}\" }";

            var inputs = "{'input': {'a':'foo', 'b':'bar', 'c': 'baz'} }".ToInputs();

            AssertQuerySuccess(_query, expected, inputs);
        }

        [Test]
        public void uses_default_value_when_not_provided()
        {
            var queryWithDefaults = @"
                query q($input: TestInputObject = {a: ""foo"", b: [""bar""] c: ""baz""}) {
                  fieldWithObjectInput(input: $input)
                }
            ";

            var expected = "{ \"fieldWithObjectInput\": \"{\\\"a\\\":\\\"foo\\\",\\\"b\\\":[\\\"bar\\\"],\\\"c\\\":\\\"baz\\\"}\" }";

            AssertQuerySuccess(queryWithDefaults, expected);
        }

        [Test]
        public void executes_with_complex_scalar_input()
        {
            var expected = "{ \"fieldWithObjectInput\": \"{\\\"c\\\":\\\"foo\\\",\\\"d\\\":\\\"DeserializedValue\\\"}\" }";

            var inputs = "{'input': {'c': 'foo', 'd': 'SerializedValue'} }".ToInputs();

            AssertQuerySuccess(_query, expected, inputs);
        }

        [Test]
        public void errors_on_null_for_nested_non_null()
        {
            string expected = null;

            var inputs = "{'input': {'a': 'foo', 'b': 'bar', 'c': null} }".ToInputs();

            var result = AssertQueryWithErrors(_query, expected, inputs, expectedErrorCount: 1);

            var caughtError = result.Errors.Single();
            caughtError.ShouldNotBeNull();
            caughtError?.InnerException.ShouldNotBeNull();
            caughtError?.InnerException.Message.ShouldEqual("Variable '$input' expected value of type 'TestInputObject'.");
        }

        [Test]
        public void errors_on_incorrect_type()
        {
            string expected = null;

            var inputs = "{'input': 'foo bar'}".ToInputs();

            var result = AssertQueryWithErrors(_query, expected, inputs, expectedErrorCount: 1);

            var caughtError = result.Errors.Single();

            caughtError.ShouldNotBeNull();
            caughtError?.InnerException.ShouldNotBeNull();
            caughtError?.InnerException.Message.ShouldEqual("Variable '$input' expected value of type 'TestInputObject'.");
        }

        [Test]
        public void errors_on_omission_of_nested_non_null()
        {
            string expected = null;

            var inputs = "{'input': {'a': 'foo', 'b': 'bar'} }".ToInputs();

            var result = AssertQueryWithErrors(_query, expected, inputs, expectedErrorCount: 1);

            var caughtError = result.Errors.Single();
            caughtError.ShouldNotBeNull();
            caughtError?.InnerException.ShouldNotBeNull();
            caughtError?.InnerException.Message.ShouldEqual("Variable '$input' expected value of type 'TestInputObject'.");
        }

        [Test]
        public void errors_on_addition_of_unknown_input_field()
        {
            string expected = null;

            var inputs = "{'input': {'a': 'foo', 'b': 'bar', 'c': 'baz', 'e': 'dog'} }".ToInputs();

            var result = AssertQueryWithErrors(_query, expected, inputs, expectedErrorCount: 1);

            var caughtError = result.Errors.Single();
            caughtError.ShouldNotBeNull();
            caughtError?.InnerException.ShouldNotBeNull();
            caughtError?.InnerException.Message.ShouldEqual("Variable '$input' expected value of type 'TestInputObject'.");
        }

        [Test]
        public void executes_with_injected_input_variables()
        {
            var query = @"
                query q($argC: String, $argD: ComplexScalar) {
                  fieldWithObjectInput(input: { c: $argC, d: $argD })
                }
            ";

            var expected = "{ \"fieldWithObjectInput\": \"{\\\"c\\\":\\\"foo\\\",\\\"d\\\":\\\"DeserializedValue\\\"}\" }";

            var inputs = "{'argC': 'foo', 'argD': 'SerializedValue'}".ToInputs();

            AssertQuerySuccess(query, expected, inputs);
        }
    }

    public class HandlesNullableScalarsTests : QueryTestBase<VariablesSchema>
    {
        [Test]
        public void allows_nullable_inputs_to_be_ommited()
        {
            var query = @"
            {
              fieldWithNullableStringInput
            }
            ";

            var expected = @"
            {
              'fieldWithNullableStringInput': 'null'
            }
            ";

            AssertQuerySuccess(query, expected);
        }

        [Test]
        public void allows_nullable_inputs_to_be_ommited_in_a_variable()
        {
            var query = @"
                query SetsNullable($value: String) {
                  fieldWithNullableStringInput(input: $value)
                }
            ";

            var expected = @"
            {
              'fieldWithNullableStringInput': 'null'
            }
            ";

            AssertQuerySuccess(query, expected);
        }

        [Test]
        public void allows_nullable_inputs_to_be_ommited_in_an_unlisted_variable()
        {
            var query = @"
                query SetsNullable {
                  fieldWithNullableStringInput(input: $value)
                }
            ";

            var expected = @"
            {
              'fieldWithNullableStringInput': 'null'
            }
            ";

            AssertQuerySuccess(query, expected);
        }

        [Test]
        public void allows_nullable_inputs_to_be_set_to_null_in_a_variable()
        {
            var query = @"
                query SetsNullable($value: String) {
                  fieldWithNullableStringInput(input: $value)
                }
            ";

            var expected = @"
            {
              'fieldWithNullableStringInput': 'null'
            }
            ";

            var inputs = "{'value':null}".ToInputs();

            AssertQuerySuccess(query, expected, inputs);
        }

        [Test]
        public void allows_nullable_inputs_to_be_set_to_a_value_in_a_variable()
        {
            var query = @"
                query SetsNullable($value: String) {
                  fieldWithNullableStringInput(input: $value)
                }
            ";

            var expected = @"
            {
              'fieldWithNullableStringInput': 'a'
            }
            ";

            var inputs = "{'value':'a'}".ToInputs();

            AssertQuerySuccess(query, expected, inputs);
        }

        [Test]
        public void allows_nullable_inputs_to_be_set_to_a_value_directly()
        {
            var query = @"
            {
              fieldWithNullableStringInput(input: ""a"")
            }
            ";

            var expected = @"
            {
              'fieldWithNullableStringInput': 'a'
            }
            ";

            AssertQuerySuccess(query, expected);
        }
    }

    public class HandlesNonNullScalarTests : QueryTestBase<VariablesSchema>
    {
        [Test]
        public void does_not_allow_non_nullable_inputs_to_be_omitted_in_a_variable()
        {
            var query = @"
            query SetsNonNullable($value: String!) {
              fieldWithNonNullableStringInput(input: $value)
            }
            ";

            string expected = null;

            var result = AssertQueryWithErrors(query, expected, expectedErrorCount: 1);

            var caughtError = result.Errors.Single();
            caughtError.ShouldNotBeNull();
            caughtError.InnerException.ShouldNotBeNull();
            caughtError.InnerException.Message.ShouldEqual("Variable '$value' of required type 'String' was not provided.");
        }

        [Test]
        public void does_not_allow_non_nullable_inputs_to_be_set_to_null_in_a_variable()
        {
            var query = @"
            query SetsNonNullable($value: String!) {
              fieldWithNonNullableStringInput(input: $value)
            }
            ";

            string expected = null;

            var inputs = "{'value':null}".ToInputs();

            var result = AssertQueryWithErrors(query, expected, inputs, expectedErrorCount: 1);

            var caughtError = result.Errors.Single();
            caughtError.ShouldNotBeNull();
            caughtError.InnerException.ShouldNotBeNull();
            caughtError.InnerException.Message.ShouldEqual("Variable '$value' of required type 'String' was not provided.");
        }

        [Test]
        public void allows_non_nullable_inputs_to_be_set_to_a_value_in_a_variable()
        {
            var query = @"
                query SetsNullable($value: String) {
                  fieldWithNullableStringInput(input: $value)
                }
            ";

            var expected = @"
            {
              'fieldWithNullableStringInput': 'a'
            }
            ";

            var inputs = "{'value':'a'}".ToInputs();

            AssertQuerySuccess(query, expected, inputs);
        }

        [Test]
        public void allows_non_nullable_inputs_to_be_set_to_a_value_directly()
        {
            var query = @"
            {
              fieldWithNullableStringInput(input: ""a"")
            }
            ";

            var expected = @"
            {
              'fieldWithNullableStringInput': 'a'
            }
            ";

            AssertQuerySuccess(query, expected);
        }
    }

    public class ArgumentDefaultValuesTests : QueryTestBase<VariablesSchema>
    {
        [Test]
        public void when_no_argument_provided()
        {
            var query = @"
            {
              fieldWithDefaultArgumentValue
            }
            ";

            var expected = @"
            {
              'fieldWithDefaultArgumentValue': ""Hello World""
            }
            ";

            AssertQuerySuccess(query, expected);
        }

        [Test]
        public void when_nullable_variable_provided()
        {
            var query = @"
            query optionalVariable($optional:String) {
              fieldWithDefaultArgumentValue(input: $optional)
            }
            ";

            var expected = @"
            {
              'fieldWithDefaultArgumentValue': ""Hello World""
            }
            ";

            AssertQuerySuccess(query, expected);
        }

        [Test]
        public void when_argument_provided_cannot_be_parsed()
        {
            var query = @"
            {
              fieldWithDefaultArgumentValue(input: WRONG_TYPE)
            }
            ";

            var expected = @"
            {
              'fieldWithDefaultArgumentValue': ""Hello World""
            }
            ";

            AssertQuerySuccess(query, expected, rules:Enumerable.Empty<IValidationRule>());
        }
    }
}
