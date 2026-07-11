package io.github.nizos.tddguard.junit5.model;

import java.util.Objects;

/**
 * Error raised outside of any test method, typically from a JUnit5
 * {@code @BeforeAll} or {@code @AfterAll} hook. Maps to a single entry
 * in {@code test.json}'s top-level {@code unhandledErrors} array.
 */
public final class UnhandledError {
    private final String name;
    private final String message;
    private final String stack;

    public UnhandledError(String name, String message) {
        this(name, message, null);
    }

    public UnhandledError(String name, String message, String stack) {
        this.name = Objects.requireNonNull(name, "name must not be null");
        this.message = Objects.requireNonNull(message, "message must not be null");
        this.stack = stack;
    }

    public String name() {
        return name;
    }

    public String message() {
        return message;
    }

    public String stack() {
        return stack;
    }
}
