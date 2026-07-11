package io.github.nizos.tddguard.junit5.model;

import java.util.Collections;
import java.util.List;
import java.util.Objects;

/**
 * Top-level test result written to test.json.
 * Matches the TDD Guard canonical schema defined in
 * src/contracts/schemas/reporterSchemas.ts.
 */
public final class TestResult {
    private final List<TestModule> testModules;
    private final String reason;
    private final List<UnhandledError> unhandledErrors;

    public TestResult(List<TestModule> testModules) {
        this(testModules, null, Collections.emptyList());
    }

    public TestResult(List<TestModule> testModules, String reason) {
        this(testModules, reason, Collections.emptyList());
    }

    public TestResult(List<TestModule> testModules, String reason, List<UnhandledError> unhandledErrors) {
        this.testModules = Objects.requireNonNull(testModules, "testModules must not be null");
        this.reason = reason;
        this.unhandledErrors = Objects.requireNonNull(unhandledErrors, "unhandledErrors must not be null");
    }

    public List<TestModule> testModules() {
        return testModules;
    }

    public String reason() {
        return reason;
    }

    public List<UnhandledError> unhandledErrors() {
        return unhandledErrors;
    }
}
