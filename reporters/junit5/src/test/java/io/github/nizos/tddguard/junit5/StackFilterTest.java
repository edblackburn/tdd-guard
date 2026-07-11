package io.github.nizos.tddguard.junit5;

import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNull;

class StackFilterTest {

    @Test
    void returnsNullForNullStackTrace() {
        assertNull(StackFilter.firstUserFrame((StackTraceElement[]) null));
    }

    @Test
    void returnsNullForEmptyStackTrace() {
        assertNull(StackFilter.firstUserFrame(new StackTraceElement[0]));
    }

    @Test
    void returnsFirstUserFrameAsToStringValue() {
        StackTraceElement[] frames = new StackTraceElement[] {
                user("com.example.MyTest", "shouldFail", "MyTest.java", 42)
        };

        assertEquals(
                "com.example.MyTest.shouldFail(MyTest.java:42)",
                StackFilter.firstUserFrame(frames));
    }

    @Test
    void skipsJdkInternalFramesAndJunitFrames() {
        StackTraceElement[] frames = new StackTraceElement[] {
                user("org.opentest4j.AssertionFailedError", "<init>", "AssertionFailedError.java", 33),
                user("org.junit.jupiter.api.AssertionUtils", "fail", "AssertionUtils.java", 39),
                user("jdk.internal.reflect.DirectMethodHandleAccessor", "invoke", "DirectMethodHandleAccessor.java", 103),
                user("java.lang.reflect.Method", "invoke", "Method.java", 580),
                user("com.example.MyTest", "shouldFail", "MyTest.java", 10),
                user("org.junit.platform.engine.support.hierarchical.NodeTestTask", "execute", "NodeTestTask.java", 151)
        };

        assertEquals(
                "com.example.MyTest.shouldFail(MyTest.java:10)",
                StackFilter.firstUserFrame(frames));
    }

    @Test
    void skipsGradleAndGradleWorkerFrames() {
        StackTraceElement[] frames = new StackTraceElement[] {
                user("org.gradle.api.internal.tasks.testing.junitplatform.JUnitPlatformTestClassProcessor", "stop", "JUnitPlatformTestClassProcessor.java", 99),
                user("worker.org.gradle.process.internal.worker.child.SystemApplicationClassLoaderWorker", "call", "SystemApplicationClassLoaderWorker.java", 121),
                user("com.example.MyTest", "shouldFail", "MyTest.java", 7)
        };

        assertEquals(
                "com.example.MyTest.shouldFail(MyTest.java:7)",
                StackFilter.firstUserFrame(frames));
    }

    @Test
    void skipsReporterOwnFrames() {
        StackTraceElement[] frames = new StackTraceElement[] {
                user("io.github.nizos.tddguard.junit5.TddGuardListener", "executionFinished", "TddGuardListener.java", 64),
                user("io.github.nizos.tddguard.junit5.TestResultCollector", "recordFailed", "TestResultCollector.java", 30),
                user("com.example.MyTest", "shouldFail", "MyTest.java", 12)
        };

        assertEquals(
                "com.example.MyTest.shouldFail(MyTest.java:12)",
                StackFilter.firstUserFrame(frames));
    }

    @Test
    void returnsNullWhenAllFramesAreFrameworkInternal() {
        StackTraceElement[] frames = new StackTraceElement[] {
                user("org.junit.jupiter.api.AssertionUtils", "fail", "AssertionUtils.java", 39),
                user("org.opentest4j.AssertionFailedError", "<init>", "AssertionFailedError.java", 33),
                user("jdk.internal.reflect.DirectMethodHandleAccessor", "invoke", "DirectMethodHandleAccessor.java", 103)
        };

        assertNull(StackFilter.firstUserFrame(frames));
    }

    @Test
    void worksThroughThrowableOverload() {
        Throwable t = new Throwable();
        t.setStackTrace(new StackTraceElement[] {
                user("org.junit.jupiter.api.AssertionUtils", "fail", "AssertionUtils.java", 39),
                user("com.example.MyTest", "shouldFail", "MyTest.java", 5)
        });

        assertEquals(
                "com.example.MyTest.shouldFail(MyTest.java:5)",
                StackFilter.firstUserFrame(t));
    }

    @Test
    void throwableOverloadReturnsNullForNull() {
        assertNull(StackFilter.firstUserFrame((Throwable) null));
    }

    @Test
    void skipsAssertjMockitoHamcrestFrames() {
        StackTraceElement[] frames = new StackTraceElement[] {
                user("org.assertj.core.api.AbstractAssert", "isEqualTo", "AbstractAssert.java", 234),
                user("org.mockito.internal.verification.MockAwareVerificationMode", "verify", "MockAwareVerificationMode.java", 26),
                user("org.hamcrest.MatcherAssert", "assertThat", "MatcherAssert.java", 18),
                user("com.example.MyTest", "shouldFail", "MyTest.java", 24)
        };

        assertEquals(
                "com.example.MyTest.shouldFail(MyTest.java:24)",
                StackFilter.firstUserFrame(frames));
    }

    @Test
    void skipsTestngFrames() {
        StackTraceElement[] frames = new StackTraceElement[] {
                user("org.testng.Assert", "assertEquals", "Assert.java", 87),
                user("com.example.MyTest", "shouldFail", "MyTest.java", 15)
        };

        assertEquals(
                "com.example.MyTest.shouldFail(MyTest.java:15)",
                StackFilter.firstUserFrame(frames));
    }

    @Test
    void skipsSpringFrames() {
        StackTraceElement[] frames = new StackTraceElement[] {
                user("org.springframework.test.context.junit.jupiter.SpringExtension", "beforeEach", "SpringExtension.java", 215),
                user("org.springframework.aop.framework.ReflectiveMethodInvocation", "proceed", "ReflectiveMethodInvocation.java", 186),
                user("com.example.MyTest", "shouldFail", "MyTest.java", 33)
        };

        assertEquals(
                "com.example.MyTest.shouldFail(MyTest.java:33)",
                StackFilter.firstUserFrame(frames));
    }

    private static StackTraceElement user(String declaringClass, String method, String file, int line) {
        return new StackTraceElement(declaringClass, method, file, line);
    }
}
