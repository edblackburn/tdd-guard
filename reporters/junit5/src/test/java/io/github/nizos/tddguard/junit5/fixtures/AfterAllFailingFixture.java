package io.github.nizos.tddguard.junit5.fixtures;

import org.junit.jupiter.api.AfterAll;
import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.assertEquals;

/**
 * Fixture executed by {@code UnhandledErrorsIntegrationTest} via the
 * JUnit Platform Launcher API. Not part of the regular test suite —
 * excluded from {@code ./gradlew test} via {@code tasks.test.exclude}
 * in {@code build.gradle.kts} so the deliberately failing {@code @AfterAll}
 * hook does not break the outer build.
 */
public class AfterAllFailingFixture {

    @Test
    void shouldPass() {
        assertEquals(5, 2 + 3);
    }

    @AfterAll
    static void teardownThatFails() {
        throw new RuntimeException("@AfterAll exploded");
    }
}
