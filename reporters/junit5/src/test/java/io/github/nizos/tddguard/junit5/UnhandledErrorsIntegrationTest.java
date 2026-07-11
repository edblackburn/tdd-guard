package io.github.nizos.tddguard.junit5;

import io.github.nizos.tddguard.junit5.fixtures.AfterAllFailingFixture;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.io.TempDir;
import org.junit.platform.engine.discovery.DiscoverySelectors;
import org.junit.platform.launcher.Launcher;
import org.junit.platform.launcher.LauncherDiscoveryRequest;
import org.junit.platform.launcher.core.LauncherDiscoveryRequestBuilder;
import org.junit.platform.launcher.core.LauncherFactory;

import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;

import static org.junit.jupiter.api.Assertions.assertTrue;

/**
 * Drives the JUnit Platform Launcher API end-to-end against a fixture whose
 * {@code @AfterAll} hook throws, mirroring the Ruby reporters'
 * {@code unhandled_errors_integration_spec.rb}. Detects regressions if
 * JUnit Platform changes how container-level failures are surfaced.
 */
class UnhandledErrorsIntegrationTest {

    @Test
    void capturesAfterAllHookFailureAsUnhandledError(@TempDir Path tmp) throws IOException {
        TddGuardListener listener = new TddGuardListener(
                new ProjectRootResolver(name -> tmp.toString(), () -> tmp),
                new TestJsonWriter(),
                name -> tmp.toString());

        LauncherDiscoveryRequest request = LauncherDiscoveryRequestBuilder.request()
                .selectors(DiscoverySelectors.selectClass(AfterAllFailingFixture.class))
                .build();

        Launcher launcher = LauncherFactory.create();
        launcher.execute(request, listener);

        Path testJson = tmp.resolve(".claude/tdd-guard/data/test.json");
        assertTrue(Files.exists(testJson),
                "test.json was not written — listener wiring may be broken");
        String content = Files.readString(testJson);

        assertTrue(content.contains("\"unhandledErrors\""),
                "unhandledErrors key missing — container failure capture may be broken");
        assertTrue(content.contains("\"name\": \"java.lang.RuntimeException\""),
                "expected RuntimeException name in unhandled error");
        assertTrue(content.contains("\"message\": \"@AfterAll exploded\""),
                "expected the original @AfterAll exception message");
        assertTrue(content.contains("\"reason\": \"failed\""),
                "reason should be failed when unhandledErrors are present");
        assertTrue(content.contains("\"state\": \"passed\""),
                "the passing test inside the fixture should still appear as passed");
    }
}
