package parser

import (
	"bufio"
	"encoding/json"
	"strings"
	"testing"
)

// Test fixtures - common JSON events
const (
	passEvent = `{"Action":"pass","Package":"example.com/pkg","Test":"TestAdd"}`
	failEvent = `{"Action":"fail","Package":"example.com/pkg","Test":"TestFail"}`
	runEvent  = `{"Action":"run","Package":"example.com/pkg","Test":"TestAdd"}`

	// Race condition test output fixture
	raceConditionOutput = `=== PAUSE TestProcessSessionStartWorksWithClear
=== CONT  TestProcessSessionStartWorksWithClear
github.com/rs/zerolog.(*Event).msg()
/home/callan/go/pkg/mod/github.com/rs/zerolog@v1.34.0/event.go:151 +0x419
github.com/rs/zerolog.(*Event).Msg()
/home/callan/go/pkg/mod/github.com/rs/zerolog@v1.34.0/event.go:110 +0x2ee
github.com/wizzomafizzo/bumpers/internal/cli.(*App).ProcessUserPrompt()
/home/callan/dev/bumpers/internal/cli/commands.go:51 +0x2bb
github.com/wizzomafizzo/bumpers/internal/cli.TestProcessUserPromptWithCommandGeneration()
/home/callan/dev/bumpers/internal/cli/app_test.go:1534 +0x149
testing.tRunner()
/usr/lib/golang/src/testing/testing.go:1792 +0x225
==================
WARNING: DATA RACE
Read at 0x00c00013e000 by goroutine 39:
strings.(*Builder).copyCheck()
/usr/lib/golang/src/strings/builder.go:27 +0x37
strings.(*Builder).Write()
/usr/lib/golang/src/strings/builder.go:82 +0x32
==================
testing.go:1490: race detected during execution of test`
)

func TestParser(t *testing.T) {
	t.Run("Creation", func(t *testing.T) {
		t.Run("creates new parser", func(t *testing.T) {
			parser := NewParser()
			if parser == nil {
				t.Fatal("Expected parser to be created")
			}
		})

		t.Run("returns empty results initially", func(t *testing.T) {
			parser := NewParser()
			results := parser.GetResults()
			if results == nil {
				t.Fatal("Expected results to be non-nil")
			}
			if len(results) != 0 {
				t.Fatalf("Expected empty results, got %d items", len(results))
			}
		})
	})

	t.Run("Parsing", func(t *testing.T) {
		t.Run("accepts io.Reader", func(t *testing.T) {
			parser := NewParser()
			err := parser.Parse(strings.NewReader(""))
			if err != nil {
				t.Fatalf("Parse returned error: %v", err)
			}
		})

		t.Run("handles invalid JSON", func(t *testing.T) {
			results := parseJSON(t, "not json")
			if len(results) != 0 {
				t.Fatal("Expected no results for invalid JSON")
			}
		})
	})

	t.Run("Recording tests", func(t *testing.T) {
		t.Run("records passing test", func(t *testing.T) {
			results := parseJSON(t, passEvent)

			if len(results) != 1 {
				t.Fatalf("Expected 1 package, got %d", len(results))
			}

			tests := getPackageTests(t, results, "example.com/pkg")
			if _, exists := tests["TestAdd"]; !exists {
				t.Fatal("Expected test 'TestAdd' to be recorded")
			}
		})

		t.Run("records failing test", func(t *testing.T) {
			results := parseJSON(t, failEvent)
			tests := getPackageTests(t, results, "example.com/pkg")

			if _, exists := tests["TestFail"]; !exists {
				t.Fatal("Expected test 'TestFail' to be recorded")
			}
		})

		t.Run("ignores non-terminal actions", func(t *testing.T) {
			results := parseJSON(t, runEvent)

			// Package should exist but with no tests
			if tests, exists := results["example.com/pkg"]; exists {
				if _, hasTest := tests["TestAdd"]; hasTest {
					t.Fatal("Should not record test with 'run' action")
				}
			}
		})
	})

	t.Run("Test states", func(t *testing.T) {
		testCases := []struct {
			name     string
			input    string
			testName string
			expected TestState
		}{
			{"passing test", passEvent, "TestAdd", StatePassed},
			{"failing test", failEvent, "TestFail", StateFailed},
		}

		for _, tc := range testCases {
			t.Run(tc.name, func(t *testing.T) {
				results := parseJSON(t, tc.input)
				tests := getPackageTests(t, results, "example.com/pkg")

				state, exists := tests[tc.testName]
				if !exists {
					t.Fatalf("Test %s not found", tc.testName)
				}
				if state != tc.expected {
					t.Errorf("Expected state %s, got %v", tc.expected, state)
				}
			})
		}
	})

	t.Run("Multiple tests", func(t *testing.T) {
		input := strings.Join([]string{
			`{"Action":"pass","Package":"example.com/pkg","Test":"TestOne"}`,
			`{"Action":"fail","Package":"example.com/pkg","Test":"TestTwo"}`,
			`{"Action":"pass","Package":"example.com/pkg","Test":"TestThree"}`,
		}, "\n")

		results := parseJSON(t, input)
		tests := getPackageTests(t, results, "example.com/pkg")

		if len(tests) != 3 {
			t.Fatalf("Expected 3 tests, got %d", len(tests))
		}

		// Verify individual test states
		expectedStates := map[string]TestState{
			"TestOne":   StatePassed,
			"TestTwo":   StateFailed,
			"TestThree": StatePassed,
		}

		for name, expectedState := range expectedStates {
			if state, exists := tests[name]; !exists {
				t.Errorf("Test %s not found", name)
			} else if state != expectedState {
				t.Errorf("Test %s: expected state %s, got %v", name, expectedState, state)
			}
		}
	})

	t.Run("Multiple packages", func(t *testing.T) {
		input := strings.Join([]string{
			`{"Action":"pass","Package":"example.com/pkg1","Test":"TestA"}`,
			`{"Action":"pass","Package":"example.com/pkg2","Test":"TestB"}`,
		}, "\n")

		results := parseJSON(t, input)

		if len(results) != 2 {
			t.Fatalf("Expected 2 packages, got %d", len(results))
		}

		// Verify both packages exist
		for _, pkg := range []string{"example.com/pkg1", "example.com/pkg2"} {
			if _, exists := results[pkg]; !exists {
				t.Errorf("Package %s not found", pkg)
			}
		}
	})

	t.Run("Subtest filtering", func(t *testing.T) {
		t.Run("filters out parent test when one subtest exists", func(t *testing.T) {
			// Simplest case: parent with one child
			input := strings.Join([]string{
				`{"Action":"pass","Package":"example.com/pkg","Test":"TestParent"}`,
				`{"Action":"pass","Package":"example.com/pkg","Test":"TestParent/Child"}`,
			}, "\n")

			results := parseJSON(t, input)
			tests := getPackageTests(t, results, "example.com/pkg")

			// Should only have the child test, not the parent
			if _, exists := tests["TestParent"]; exists {
				t.Error("Expected TestParent to be filtered out when child exists")
			}
		})

		t.Run("keeps test without children", func(t *testing.T) {
			input := strings.Join([]string{
				`{"Action":"pass","Package":"example.com/pkg","Test":"TestSimple"}`,
				`{"Action":"pass","Package":"example.com/pkg","Test":"TestParent"}`,
				`{"Action":"pass","Package":"example.com/pkg","Test":"TestParent/Child"}`,
			}, "\n")

			results := parseJSON(t, input)
			tests := getPackageTests(t, results, "example.com/pkg")

			// TestSimple has no children, should be kept
			if _, exists := tests["TestSimple"]; !exists {
				t.Error("Expected TestSimple to exist (has no children)")
			}
		})

		t.Run("filters parent with multiple children", func(t *testing.T) {
			input := strings.Join([]string{
				`{"Action":"pass","Package":"example.com/pkg","Test":"TestParent"}`,
				`{"Action":"pass","Package":"example.com/pkg","Test":"TestParent/Child1"}`,
				`{"Action":"pass","Package":"example.com/pkg","Test":"TestParent/Child2"}`,
			}, "\n")

			results := parseJSON(t, input)
			tests := getPackageTests(t, results, "example.com/pkg")

			// Parent should be filtered out
			if _, exists := tests["TestParent"]; exists {
				t.Error("Expected TestParent to be filtered out when multiple children exist")
			}
		})

		t.Run("filters intermediate level in nested tests", func(t *testing.T) {
			input := strings.Join([]string{
				`{"Action":"pass","Package":"example.com/pkg","Test":"TestAPI"}`,
				`{"Action":"pass","Package":"example.com/pkg","Test":"TestAPI/Users"}`,
				`{"Action":"pass","Package":"example.com/pkg","Test":"TestAPI/Users/Create"}`,
			}, "\n")

			results := parseJSON(t, input)
			tests := getPackageTests(t, results, "example.com/pkg")

			// TestAPI/Users should be filtered out (has child)
			if _, exists := tests["TestAPI/Users"]; exists {
				t.Error("Expected TestAPI/Users to be filtered out when it has children")
			}
		})
	})

	t.Run("Test output capture", func(t *testing.T) {
		t.Run("captures output for specific test", func(t *testing.T) {
			input := strings.Join([]string{
				`{"Action":"run","Package":"example.com/pkg","Test":"ExampleTest"}`,
				`{"Action":"output","Package":"example.com/pkg","Test":"ExampleTest","Output":"=== RUN   ExampleTest\n"}`,
				`{"Action":"output","Package":"example.com/pkg","Test":"ExampleTest","Output":"    main_test.go:10: Expected 6 but got 5\n"}`,
				`{"Action":"output","Package":"example.com/pkg","Test":"ExampleTest","Output":"--- FAIL: ExampleTest (0.00s)\n"}`,
				`{"Action":"fail","Package":"example.com/pkg","Test":"ExampleTest","Elapsed":0}`,
			}, "\n")

			output := parseAndGetOutput(t, input)
			if output == "" {
				t.Fatal("Expected test output to be captured")
			}
		})

		t.Run("returns empty string for non-existent test", func(t *testing.T) {
			parser := NewParser()

			testOutput := parser.GetTestOutput("example.com/pkg", "NonExistentTest")
			if testOutput != "" {
				t.Fatalf("Expected empty string for non-existent test, got %q", testOutput)
			}
		})

		t.Run("returns empty string when test exists but has no output", func(t *testing.T) {
			input := `{"Action":"pass","Package":"example.com/pkg","Test":"ExampleTest"}`

			output := parseAndGetOutput(t, input)
			if output != "" {
				t.Fatalf("Expected empty string for test without output, got %q", output)
			}
		})

		t.Run("returns actual output content", func(t *testing.T) {
			input := strings.Join([]string{
				`{"Action":"output","Package":"example.com/pkg","Test":"ExampleTest","Output":"Expected 6 but got 5\n"}`,
				`{"Action":"fail","Package":"example.com/pkg","Test":"ExampleTest"}`,
			}, "\n")

			output := parseAndGetOutput(t, input)
			expected := "Expected 6 but got 5"
			if output != expected {
				t.Fatalf("Expected %q, got %q", expected, output)
			}
		})

		t.Run("concatenates multiple output lines", func(t *testing.T) {
			input := strings.Join([]string{
				`{"Action":"output","Package":"example.com/pkg","Test":"ExampleTest","Output":"Line 1\n"}`,
				`{"Action":"output","Package":"example.com/pkg","Test":"ExampleTest","Output":"Line 2\n"}`,
				`{"Action":"fail","Package":"example.com/pkg","Test":"ExampleTest"}`,
			}, "\n")

			output := parseAndGetOutput(t, input)
			expected := "Line 1\nLine 2"
			if output != expected {
				t.Fatalf("Expected %q, got %q", expected, output)
			}
		})

		t.Run("excludes RUN lines from output", func(t *testing.T) {
			input := strings.Join([]string{
				`{"Action":"output","Package":"example.com/pkg","Test":"ExampleTest","Output":"=== RUN   ExampleTest\n"}`,
				`{"Action":"output","Package":"example.com/pkg","Test":"ExampleTest","Output":"Error message\n"}`,
				`{"Action":"fail","Package":"example.com/pkg","Test":"ExampleTest"}`,
			}, "\n")

			output := parseAndGetOutput(t, input)
			if strings.Contains(output, "=== RUN") {
				t.Fatalf("Output should not contain RUN line, got %q", output)
			}
		})

		t.Run("excludes FAIL lines from output", func(t *testing.T) {
			input := strings.Join([]string{
				`{"Action":"output","Package":"example.com/pkg","Test":"ExampleTest","Output":"Error message\n"}`,
				`{"Action":"output","Package":"example.com/pkg","Test":"ExampleTest","Output":"--- FAIL: ExampleTest (0.00s)\n"}`,
				`{"Action":"fail","Package":"example.com/pkg","Test":"ExampleTest"}`,
			}, "\n")

			output := parseAndGetOutput(t, input)
			if strings.Contains(output, "--- FAIL") {
				t.Fatalf("Output should not contain FAIL line, got %q", output)
			}
		})

		t.Run("trims leading whitespace from error lines", func(t *testing.T) {
			input := strings.Join([]string{
				`{"Action":"output","Package":"example.com/pkg","Test":"ExampleTest","Output":"    test.go:10: Expected 6 but got 5\n"}`,
				`{"Action":"fail","Package":"example.com/pkg","Test":"ExampleTest"}`,
			}, "\n")

			output := parseAndGetOutput(t, input)
			expected := "test.go:10: Expected 6 but got 5"
			if output != expected {
				t.Fatalf("Expected %q, got %q", expected, output)
			}
		})

		t.Run("trims trailing newline from output", func(t *testing.T) {
			input := strings.Join([]string{
				`{"Action":"output","Package":"example.com/pkg","Test":"ExampleTest","Output":"test.go:10: Error message\n"}`,
				`{"Action":"fail","Package":"example.com/pkg","Test":"ExampleTest"}`,
			}, "\n")

			output := parseAndGetOutput(t, input)
			expected := "test.go:10: Error message"
			if output != expected {
				t.Fatalf("Expected %q, got %q", expected, output)
			}
		})
	})

	t.Run("Build events", func(t *testing.T) {
		t.Run("TestEvent includes ImportPath field", func(t *testing.T) {
			input := `{"Action":"build-output","ImportPath":"example.com/pkg","Output":"# example.com/pkg\n"}`

			reader := strings.NewReader(input)
			scanner := bufio.NewScanner(reader)

			if scanner.Scan() {
				var event TestEvent
				err := json.Unmarshal(scanner.Bytes(), &event)
				if err != nil {
					t.Fatalf("Failed to unmarshal: %v", err)
				}

				if event.ImportPath != "example.com/pkg" {
					t.Errorf("Expected ImportPath to be 'example.com/pkg', got '%s'", event.ImportPath)
				}
			}
		})

		t.Run("TestEvent includes FailedBuild field", func(t *testing.T) {
			input := `{"Action":"fail","Package":"example.com/pkg","FailedBuild":"example.com/pkg"}`

			reader := strings.NewReader(input)
			scanner := bufio.NewScanner(reader)

			if scanner.Scan() {
				var event TestEvent
				err := json.Unmarshal(scanner.Bytes(), &event)
				if err != nil {
					t.Fatalf("Failed to unmarshal: %v", err)
				}

				if event.FailedBuild != "example.com/pkg" {
					t.Errorf("Expected FailedBuild to be 'example.com/pkg', got '%s'", event.FailedBuild)
				}
			}
		})

		t.Run("captures build-output events", func(t *testing.T) {
			parser := NewParser()

			input := `{"Action":"build-output","ImportPath":"example.com/pkg","Output":"compilation error\n"}`
			err := parser.Parse(strings.NewReader(input))
			if err != nil {
				t.Fatalf("Parse failed: %v", err)
			}

			// We need a way to verify the build output was captured
			// For now, let's add a GetBuildFailure method
			buildOutput := parser.GetBuildFailure("example.com/pkg")
			if buildOutput != "compilation error\n" {
				t.Errorf("Expected build output 'compilation error\\n', got %q", buildOutput)
			}
		})

		t.Run("creates CompilationError test for build failure", func(t *testing.T) {
			parser := NewParser()

			input := `{"Action":"fail","Package":"example.com/pkg","FailedBuild":"example.com/pkg"}`
			err := parser.Parse(strings.NewReader(input))
			if err != nil {
				t.Fatalf("Parse failed: %v", err)
			}

			results := parser.GetResults()
			tests := results["example.com/pkg"]

			state, exists := tests["CompilationError"]
			if !exists {
				t.Fatal("Expected CompilationError test to be created")
			}

			if state != StateFailed {
				t.Errorf("Expected CompilationError to have state 'failed', got %v", state)
			}
		})

		t.Run("GetTestOutput returns build failure for CompilationError", func(t *testing.T) {
			parser := NewParser()

			// First capture build output
			buildOutput := `{"Action":"build-output","ImportPath":"example.com/pkg","Output":"undefined: someFunc\n"}`
			err := parser.Parse(strings.NewReader(buildOutput))
			if err != nil {
				t.Fatalf("Parse failed: %v", err)
			}

			// Then process the build failure
			failEvent := `{"Action":"fail","Package":"example.com/pkg","FailedBuild":"example.com/pkg"}`
			err = parser.Parse(strings.NewReader(failEvent))
			if err != nil {
				t.Fatalf("Parse failed: %v", err)
			}

			// GetTestOutput should return the build failure message for CompilationError
			output := parser.GetTestOutput("example.com/pkg", "CompilationError")
			if output != "undefined: someFunc" {
				t.Errorf("Expected 'undefined: someFunc', got %q", output)
			}
		})
	})

	t.Run("Package-level output", func(t *testing.T) {
		t.Run("captures error output messages", func(t *testing.T) {
			// When compilation fails, Go emits output events before the fail event
			input := strings.Join([]string{
				`{"Action":"output","Package":"example.com/pkg","Output":"# example.com/pkg\n"}`,
				`{"Action":"output","Package":"example.com/pkg","Output":"./main.go:3:8: package foo is not in std\n"}`,
				`{"Action":"fail","Package":"example.com/pkg","Elapsed":0.001}`,
			}, "\n")

			parser := NewParser()
			err := parser.Parse(strings.NewReader(input))
			if err != nil {
				t.Fatalf("Parse failed: %v", err)
			}

			// Check if we have error output stored
			errorOutput := parser.GetErrorOutput("example.com/pkg")
			if errorOutput == "" {
				t.Fatal("Expected error output to be captured")
			}
		})

		t.Run("marks package as failed when package-level fail with no tests", func(t *testing.T) {
			// This simulates a build failure that produces JSON output
			// The package fails but has no test entries
			input := `{"Action":"fail","Package":"example.com/pkg","Elapsed":0}`

			parser := NewParser()
			err := parser.Parse(strings.NewReader(input))
			if err != nil {
				t.Fatalf("Parse failed: %v", err)
			}

			results := parser.GetResults()
			if pkg, exists := results["example.com/pkg"]; exists {
				if pkg["CompilationError"] != StateFailed {
					t.Fatal("Expected CompilationError to be failed for package-level failure")
				}
			} else {
				t.Fatal("Expected package to exist in results")
			}
		})

		t.Run("combines multiple output lines", func(t *testing.T) {
			input := strings.Join([]string{
				`{"Action":"output","Package":"example.com/pkg","Output":"# example.com/pkg\n"}`,
				`{"Action":"output","Package":"example.com/pkg","Output":"./main.go:3:8: package foo is not in std\n"}`,
				`{"Action":"output","Package":"example.com/pkg","Output":"./main.go:4:8: package bar is not in std\n"}`,
				`{"Action":"fail","Package":"example.com/pkg","Elapsed":0.001}`,
			}, "\n")

			parser := NewParser()
			err := parser.Parse(strings.NewReader(input))
			if err != nil {
				t.Fatalf("Parse failed: %v", err)
			}

			errorOutput := parser.GetErrorOutput("example.com/pkg")
			expected := "# example.com/pkg\n./main.go:3:8: package foo is not in std\n./main.go:4:8: package bar is not in std\n"
			if errorOutput != expected {
				t.Errorf("Expected output:\n%q\nGot:\n%q", expected, errorOutput)
			}
		})
	})
}

func TestTruncateTestOutput(t *testing.T) {
	t.Run("truncates race condition output", func(t *testing.T) {
		truncated := truncateTestOutput(raceConditionOutput)

		// Should contain race detection info but not stack traces
		if !strings.Contains(truncated, "race detected during execution of test") {
			t.Error("Should preserve race detection message")
		}

		// Should not contain verbose stack traces
		if strings.Contains(truncated, "/home/callan/go/pkg/mod/github.com/rs/zerolog") {
			t.Error("Should not contain stack trace paths")
		}

		// Should be much shorter
		if len(truncated) > 200 {
			t.Errorf("Truncated output too long: %d chars, expected under 200", len(truncated))
		}
	})

	t.Run("preserves short output unchanged", func(t *testing.T) {
		shortOutput := "TestFailed: expected 5, got 3"
		result := truncateTestOutput(shortOutput)

		if result != shortOutput {
			t.Errorf("Expected short output unchanged, got %q", result)
		}
	})

	t.Run("truncates very long output", func(t *testing.T) {
		longOutput := strings.Repeat("This is a very long error message. ", 50) // ~1750 chars
		result := truncateTestOutput(longOutput)

		if len(result) > 600 {
			t.Errorf("Output not truncated enough: %d chars, expected under 600", len(result))
		}

		if !strings.Contains(result, "[truncated") {
			t.Error("Should indicate truncation occurred")
		}
	})

	t.Run("handles multiple race conditions", func(t *testing.T) {
		multiRaceOutput := raceConditionOutput + "\n" + raceConditionOutput
		result := truncateTestOutput(multiRaceOutput)

		// Should include the multi-race summary exactly once
		summaryCount := strings.Count(result, "Multiple race conditions detected")
		if summaryCount != 1 {
			t.Errorf("Expected 1 multi-race summary, got %d", summaryCount)
		}
	})

	t.Run("prioritizes file location when truncating many lines", func(t *testing.T) {
		// Create output with >500 chars and >5 lines where file location is in line 6
		lines := []string{
			strings.Repeat("very long test output line 1 with lots of content to exceed 500 chars ", 3),
			strings.Repeat("very long test output line 2 with lots of content to exceed 500 chars ", 3),
			strings.Repeat("very long test output line 3 with lots of content to exceed 500 chars ", 3),
			strings.Repeat("very long test output line 4 with lots of content to exceed 500 chars ", 3),
			strings.Repeat("very long test output line 5 with lots of content to exceed 500 chars ", 3),
			"    critical_test.go:123: This is the important error location", // Line 6 - would be lost!
			"test output line 7",
			"test output line 8",
		}
		longOutput := strings.Join(lines, "\n")
		t.Logf("Total length: %d chars", len(longOutput))

		result := truncateTestOutput(longOutput)
		t.Logf("Truncated output: %q", result)

		// Current implementation takes first 5 lines and truncates - file location may be lost
		if !strings.Contains(result, "critical_test.go:123") {
			t.Errorf("File location should be preserved for AI parsing, but was lost in truncation. Got: %q", result)
		}
	})

	t.Run("containsGoFileLocation detects file location patterns", func(t *testing.T) {
		testCases := []struct {
			line     string
			expected bool
		}{
			{"    test.go:123: error message", true},
			{"    test.go:123:45: error with column", true},
			{"ordinary output line", false},
			{"test.go without line number", false},
			{"some.txt:123: not a go file", false},
		}

		for _, tc := range testCases {
			result := containsGoFileLocation(tc.line)
			if result != tc.expected {
				t.Errorf("containsGoFileLocation(%q) = %v, expected %v", tc.line, result, tc.expected)
			}
		}
	})

	t.Run("selectLinesForTruncation prioritizes file locations", func(t *testing.T) {
		lines := []string{
			"line 1",
			"line 2",
			"    test.go:123: important error",
			"line 4",
			"line 5",
			"    another.go:456: another error",
		}

		selected := selectLinesForTruncation(lines, 3)

		// Should include both file location lines plus one regular line
		if len(selected) != 3 {
			t.Errorf("Expected 3 lines selected, got %d", len(selected))
		}

		// Both file locations should be kept (prioritized over middle non-location lines)
		joined := strings.Join(selected, "\n")
		if !strings.Contains(joined, "test.go:123") {
			t.Errorf("Expected test.go:123 to be kept, got: %v", selected)
		}
		if !strings.Contains(joined, "another.go:456") {
			t.Errorf("Expected another.go:456 to be kept, got: %v", selected)
		}
	})

	t.Run("preserves file location in race condition output", func(t *testing.T) {
		// Race condition output with file location - should preserve location for AI parsing
		raceOutput := `=== PAUSE TestExample
    race_test.go:25: race detected during execution of test
    Some additional race output
    More race details`

		result := truncateTestOutput(raceOutput)

		// Should preserve the file:line location for AI parsing, not just generic message
		if !strings.Contains(result, "race_test.go:25") {
			t.Errorf("Expected race condition to preserve file location for AI parsing, got: %q", result)
		}

		// Should still indicate it's a race condition
		if !strings.Contains(result, "race detected during execution of test") {
			t.Errorf("Expected race condition to preserve race detection message, got: %q", result)
		}
	})
}

// Helper functions

func parseJSON(t *testing.T, input string) Results {
	t.Helper()
	parser := NewParser()
	err := parser.Parse(strings.NewReader(input))
	if err != nil {
		t.Fatalf("Parse failed: %v", err)
	}
	return parser.GetResults()
}

func getPackageTests(t *testing.T, results Results, pkg string) PackageResults {
	t.Helper()
	tests, exists := results[pkg]
	if !exists {
		t.Fatalf("Package %s not found", pkg)
	}
	return tests
}

// parseAndGetOutput is a helper for test output capture tests
// It uses the default package "example.com/pkg" and test name ExampleTest"
func parseAndGetOutput(t *testing.T, input string) string {
	t.Helper()
	parser := NewParser()
	err := parser.Parse(strings.NewReader(input))
	if err != nil {
		t.Fatalf("Parse failed: %v", err)
	}
	return parser.GetTestOutput("example.com/pkg", "ExampleTest")
}

func TestTruncationPreservesOriginalLineOrder(t *testing.T) {
	// Given: long output (>500 chars, >5 lines) with file:line markers
	// scattered between context lines
	lines := []string{
		"line 1: entering function",
		"line 2: setting up input x=42",
		"line 3: calling under_test",
		"    my_test.go:25: assertion failed",
		"line 5: after first failure",
		"line 6: cleanup attempted",
		"    my_test.go:30: second assertion failed",
		"line 8: final cleanup",
	}
	padded := make([]string, len(lines))
	for i, line := range lines {
		padded[i] = line + " " + strings.Repeat("x", 80)
	}
	output := strings.Join(padded, "\n")

	result := truncateTestOutput(output)

	// Then: file:line markers should appear in original order
	firstMarker := strings.Index(result, "my_test.go:25")
	secondMarker := strings.Index(result, "my_test.go:30")
	if firstMarker > 0 && secondMarker > 0 && firstMarker > secondMarker {
		t.Errorf("expected my_test.go:25 to appear before my_test.go:30 in original order, but got them reversed; truncated output:\n%s", result)
	}

	// And: truncation should not reorder lines such that an error appears
	// before its surrounding context
	firstLine := strings.Split(result, "\n")[0]
	if strings.Contains(firstLine, "my_test.go:") && !strings.Contains(firstLine, "line 1") {
		t.Errorf("expected truncated output to start with original context lines, not a file:line error (reordering); first line: %q", firstLine)
	}
}

func TestContainsGoFileLocationRejectsURLs(t *testing.T) {
	// Given: lines that contain ".go:" and multiple colons but are URLs,
	// not Go source file locations
	urls := []string{
		"see http://example.com/foo.go:8080/docs for details",
		"fetching from https://github.com/org/repo.go:443",
	}

	// Then: containsGoFileLocation should not match them
	for _, line := range urls {
		if containsGoFileLocation(line) {
			t.Errorf("containsGoFileLocation should not match URL-like line: %q", line)
		}
	}
}

func TestMultiRacePreservesFileLocations(t *testing.T) {
	// Given: output with multiple race warnings, each reporting a different file:line
	output := `WARNING: DATA RACE
Read at 0x00c0000a4000 by goroutine 7:
    handler.go:42: access from goroutine 7
race detected during execution of test
Previous write at 0x00c0000a4000 by goroutine 8:
    handler.go:58: write from goroutine 8
race detected during execution of test`

	result := truncateTestOutput(output)

	// Then: at least one of the original file:line locations should be preserved
	// so the agent can tell where the races happened
	if !strings.Contains(result, "handler.go:42") && !strings.Contains(result, "handler.go:58") {
		t.Errorf("expected multi-race output to preserve at least one file:line location, got: %q", result)
	}
}
