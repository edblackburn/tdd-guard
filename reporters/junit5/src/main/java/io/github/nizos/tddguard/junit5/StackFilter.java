package io.github.nizos.tddguard.junit5;

/**
 * Picks the first user-code frame from a stack trace so that {@code test.json}
 * carries a single, navigable location instead of the full noisy trace.
 *
 * <p>Mirrors {@code extract_relevant_stack} in the RSpec and Minitest reporters:
 * the goal is to find the frame the developer would jump to, skipping JDK,
 * JUnit, Gradle, and this reporter's own classes.
 */
public final class StackFilter {

    private static final String[] FRAMEWORK_PREFIXES = {
            "java.",
            "javax.",
            "jdk.",
            "sun.",
            "com.sun.",
            "org.junit.",
            "org.opentest4j.",
            "org.gradle.",
            "worker.org.gradle.",
            "org.assertj.",
            "org.mockito.",
            "org.hamcrest.",
            "org.testng.",
            "org.springframework.",
            "io.github.nizos.tddguard."
    };

    private StackFilter() {
    }

    public static String firstUserFrame(Throwable throwable) {
        if (throwable == null) {
            return null;
        }
        return firstUserFrame(throwable.getStackTrace());
    }

    public static String firstUserFrame(StackTraceElement[] frames) {
        if (frames == null) {
            return null;
        }
        for (StackTraceElement frame : frames) {
            if (isUserFrame(frame)) {
                return frame.toString();
            }
        }
        return null;
    }

    private static boolean isUserFrame(StackTraceElement frame) {
        String className = frame.getClassName();
        if (className == null) {
            return false;
        }
        for (String prefix : FRAMEWORK_PREFIXES) {
            if (className.startsWith(prefix)) {
                return false;
            }
        }
        return true;
    }
}
