# Remediation Tasks Analysis

**Project:** NovaLeave  
**Analysis date:** 2026-09-22  
**Scope:** Blocking issue, first missing business functionality issue, first security finding, first quality or verification debt issue, and first recorded decision.  
**Status:** Analysis completed; implementation not started.

> This document covers the first issue listed under **Blocking**, the first issue listed under **Business functionality that is missing**, the first issue listed under **Security findings**, the first issue listed under **Quality or verification debt**, and the first item under **Decisions already made** in `docs/Keep-in-mind.md`. Later findings remain outside the current scope.

## Contents

- [Remediation Tasks Analysis](#remediation-tasks-analysis)
  - [Contents](#contents)
  - [1. Blocking: Presentation Test Suite](#1-blocking-presentation-test-suite)
    - [1.1 Current Test State](#11-current-test-state)
    - [1.2 Identified Root Causes](#12-identified-root-causes)
    - [1.3 Blocking Impact](#13-blocking-impact)
    - [1.4 Specification Reference](#14-specification-reference)
    - [1.5 Remediation Necessity](#15-remediation-necessity)
    - [1.6 Consequences of Not Implementing the Remediation](#16-consequences-of-not-implementing-the-remediation)
    - [1.7 Benefits of Implementing the Remediation](#17-benefits-of-implementing-the-remediation)
    - [1.8 Conclusion](#18-conclusion)
  - [2. Business Functionality Missing: Auto-expiry](#2-business-functionality-missing-auto-expiry)
    - [2.1 Current State](#21-current-state)
    - [2.2 Identified Root Cause](#22-identified-root-cause)
    - [2.3 Blocking Impact](#23-blocking-impact)
    - [2.4 Specification Reference](#24-specification-reference)
    - [2.5 Recommended Implementation](#25-recommended-implementation)
    - [2.6 Remediation Necessity](#26-remediation-necessity)
    - [2.7 Consequences of Not Implementing the Remediation](#27-consequences-of-not-implementing-the-remediation)
    - [2.8 Benefits of Implementing the Remediation](#28-benefits-of-implementing-the-remediation)
    - [2.9 Conclusion](#29-conclusion)
  - [3. Security Finding: CDN Scripts Without Subresource Integrity](#3-security-finding-cdn-scripts-without-subresource-integrity)
    - [3.1 Current State](#31-current-state)
    - [3.2 Identified Root Cause](#32-identified-root-cause)
    - [3.3 Security Impact](#33-security-impact)
    - [3.4 Specification Reference](#34-specification-reference)
    - [3.5 Recommended Remediation](#35-recommended-remediation)
    - [3.6 Remediation Necessity](#36-remediation-necessity)
    - [3.7 Consequences of Not Implementing the Remediation](#37-consequences-of-not-implementing-the-remediation)
    - [3.8 Benefits of Implementing the Remediation](#38-benefits-of-implementing-the-remediation)
    - [3.9 Conclusion](#39-conclusion)
  - [4. Quality or Verification Debt: E2E Test Suite](#4-quality-or-verification-debt-e2e-test-suite)
    - [4.1 Current State](#41-current-state)
    - [4.2 Identified Root Cause](#42-identified-root-cause)
    - [4.3 Verification Impact](#43-verification-impact)
    - [4.4 Specification Reference](#44-specification-reference)
    - [4.5 Recommended Remediation](#45-recommended-remediation)
    - [4.6 Remediation Necessity](#46-remediation-necessity)
    - [4.7 Consequences of Not Implementing the Remediation](#47-consequences-of-not-implementing-the-remediation)
    - [4.8 Benefits of Implementing the Remediation](#48-benefits-of-implementing-the-remediation)
    - [4.9 Conclusion](#49-conclusion)
  - [5. Decision: Do Not Benchmark on the Docker Stack](#5-decision-do-not-benchmark-on-the-docker-stack)
    - [5.1 Current Decision](#51-current-decision)
    - [5.2 Technical Rationale](#52-technical-rationale)
    - [5.3 Project Impact](#53-project-impact)
    - [5.4 Reference and Constraint](#54-reference-and-constraint)
    - [5.5 Recommended Application](#55-recommended-application)
    - [5.6 Necessity of Respecting the Decision](#56-necessity-of-respecting-the-decision)
    - [5.7 Consequences of Ignoring the Decision](#57-consequences-of-ignoring-the-decision)
    - [5.8 Benefits of Respecting the Decision](#58-benefits-of-respecting-the-decision)
    - [5.9 Conclusion](#59-conclusion)

## 1. Blocking: Presentation Test Suite

### 1.1 Current Test State

`NovaLeave.Presentation.Tests` contains 49 presentation and integration tests, but approximately 24 to 26 fail inconsistently between identical runs.

The failures are non-deterministic: the result can change depending on test order, parallel execution, and which fixture initializes first. Tests may pass in isolation and fail when the complete suite runs.

This means the suite cannot currently provide reliable evidence about the behavior of authentication, authorization, session handling, vacation-request workflows, or approver flows.

The available evidence points primarily to test infrastructure defects rather than confirmed defects in production business logic.

### 1.2 Identified Root Causes

**Shared in-memory database with a fixed name.**

In the `Testing` environment, the application uses:

```csharp
options.UseInMemoryDatabase("NovaLeaveTestDb")
```

The fixed name allows different fixtures, including `AuthTestFixture` and `VacationTestFixture`, to resolve the same process-wide InMemory store.

As a result, test data can leak between fixtures. Users, employees, roles, and vacation requests created by one fixture may affect another fixture's setup and assertions.

The problem is reinforced by seeding guards such as:

```csharp
if (await db.Employees.AnyAsync())
{
    return;
}
```

When another fixture has already inserted an employee, the current fixture may skip its required initialization.

**Misaligned or non-existent roles.**

The application contract defines only these roles:

- `User`.
- `Approver`.

The test fixtures also attempt to use:

- `Employee`.
- `Admin`.

Those roles do not match the current application contract. `Employee` is not the application's employee role, and `Admin` is not part of the defined MVP role model.

This can produce errors such as `Role EMPLOYEE does not exist` and causes authorization tests to represent a different security model from the production application.

A related test setup problem is the use of `BuildServiceProvider()` inside fixture configuration, which creates a secondary dependency-injection container and can cause seeded services and database contexts to differ from those used by the application under test.

### 1.3 Blocking Impact

This issue blocks reliable quality validation.

The project's CI quality gate cannot be trusted while the presentation test suite is red and non-deterministic. A build may succeed while the test result changes between executions, making it impossible to determine whether a failure is caused by:

- Production code.
- Test data contamination.
- Fixture initialization order.
- Incorrect test roles.
- A real regression.

The suite is also the main regression protection for authentication and vacation-request behavior. Until it is deterministic, future changes could either appear broken because of contaminated test data or introduce real defects that remain hidden among unrelated failures.

For this reason, the test suite must be stabilized before using it as acceptance evidence or proceeding with other high-risk work such as auto-expiry, concurrency, or security hardening.

### 1.4 Specification Reference

The complete diagnosis, requirements, and success criteria are documented in:

[spec_006 - Integration Test Isolation and Role Alignment](../.specify/specs/006-integration-test-isolation/spec_006-integration-test-isolation.md)

The specification defines the following target outcomes:

- `NovaLeave.Presentation.Tests` passes 49/49 tests.
- Ten consecutive executions produce the same result.
- Test execution order does not affect the result.
- Fixtures use isolated database instances.
- Fixtures use only `User` and `Approver`.
- No application behavior changes are required to repair the test infrastructure.

### 1.5 Remediation Necessity

**Recommended necessity: 100%**

This issue is strictly necessary to resolve because it prevents the project from establishing trustworthy evidence of correctness. The remediation is localized to test isolation, fixture initialization, and role alignment, but its effect is project-wide because it restores the quality gate and regression protection for critical workflows.

No production functionality should be considered fully verified until this issue is resolved and the presentation suite produces deterministic results.

### 1.6 Consequences of Not Implementing the Remediation

If this remediation is not implemented, the application may continue to work during normal manual execution, but the project will not have reliable automated evidence that it continues to work after changes.

The main consequences are:

- CI/CD results will remain unreliable because the same code can produce different test outcomes.
- Real regressions in authentication, authorization, sessions, roles, and vacation-request workflows may remain hidden.
- False failures caused by shared test data will continue to consume investigation and development time.
- Developers may modify correct production code in response to failures caused by test contamination.
- Future features will be built on top of an unstable validation baseline.
- The project will not be able to confidently satisfy its quality gate or certify the affected workflows.

The absence of this fix does not necessarily prevent the application from starting or serving users. It prevents the team from reliably proving that the application remains correct as the codebase evolves.

### 1.7 Benefits of Implementing the Remediation

Implementing the remediation will restore the test suite as a dependable quality control mechanism.

The main benefits are:

- Tests will run with isolated data and will no longer depend on fixture order or parallel execution.
- A test failure will provide a more trustworthy signal of a real defect.
- The CI/CD quality gate will be able to detect regressions consistently.
- Authentication and authorization tests will use the same `User` and `Approver` roles as the application.
- Debugging time will decrease because failures will no longer be obscured by cross-fixture contamination.
- Future work such as auto-expiry, concurrency, security hardening, and E2E validation will have a stable foundation.
- The fix can remain localized to test infrastructure without changing production business behavior.

The direct benefit for end users is indirect but important: future changes to security-sensitive workflows can be validated more reliably before they reach users.

### 1.8 Conclusion

The application can function correctly without this remediation in a normal execution, but the project cannot reliably demonstrate that correctness through its automated presentation tests.

The problem is therefore a quality and maintainability blocker rather than an immediate user-facing runtime failure. Its 100% remediation necessity is justified because the unstable suite undermines CI/CD, regression protection, and confidence in future changes.

Implementing the fix will not add new business functionality, but it will restore the foundation needed to verify existing functionality and safely develop the remaining project work.




## 2. Business Functionality Missing: Auto-expiry

### 2.1 Current State

The `VacationRequest` domain entity already contains the `Expire()` transition, and domain-level expiry tests exist. However, the application does not currently invoke that method in production.

The following implementation pieces are missing:

- No `AutoExpiryJob` or equivalent background process exists.
- No `IHostedService` is registered to inspect pending requests periodically.
- `EXPIRY_DAYS` is documented as a configuration value but is not bound to an operational process.
- No integration test verifies expiry against the application and database.
- No E2E test verifies the expired status through the user workflow.
- No robustness test verifies invalid configuration or idempotent execution.

### 2.2 Identified Root Cause

The domain state machine supports `Pending -> Expired`, but the infrastructure and application layers do not contain the scheduled orchestration needed to trigger that transition. The functionality was partially implemented at the domain level but never connected to a running background process.

### 2.3 Blocking Impact

This gap allows a request to remain in `Pending` indefinitely, even after the configured unresolved-request timeout should have elapsed.

The consequences include:

- Reserved vacation days may remain unavailable longer than intended.
- Approvers may see stale requests indefinitely.
- Employees may be unable to recover days reserved by a request that is no longer being processed.
- The system cannot enforce the product decision that auto-expiry replaces auto-escalation.
- The request lifecycle is incomplete even though the domain contains an `Expired` state.

This is not merely a missing background task. It leaves the business process without a mechanism to recover from an unresponsive approver and makes the configured expiry policy ineffective.

### 2.4 Specification Reference

The requirement is defined in the vacation-request specification:

- `FR-016`: pending requests must expire after the configured number of working days.
- `D-005` and `D-006`: auto-expiry is the selected design decision and must replace auto-escalation.
- `T611`: integration test.
- `T612`: `AutoExpiryJob` implementation.
- `T613`: service registration and configuration binding.
- `T614`: E2E validation.
- `T647`: invalid-configuration and idempotency tests.

Reference: [spec_001 vacation-request tasks](../.specify/specs/001-vacation-request/tasks.md).

### 2.5 Recommended Implementation

Implement a hosted background process that:

- Reads and validates `EXPIRY_DAYS`.
- Finds only eligible `Pending` requests.
- Applies the configured working-day rule.
- Transitions each request to `Expired`.
- Releases the employee's reserved days.
- Writes the corresponding audit record.
- Performs the state change, balance update, and audit write atomically.
- Is idempotent and does not create duplicate transitions.
- Logs a critical configuration error and leaves requests unchanged when `EXPIRY_DAYS` is missing or invalid.

### 2.6 Remediation Necessity

**Recommended necessity: 95%.**

This functionality is highly necessary because it enforces a resolved product decision and completes the request lifecycle. The percentage is below 100% only because the application can still process ordinary requests manually; however, the system is not behaviorally complete while pending requests can remain unresolved forever.

### 2.7 Consequences of Not Implementing the Remediation

- Pending requests can remain active indefinitely.
- Reserved balances can remain blocked.
- The application may show inaccurate availability to employees.
- Approver work queues can contain stale requests.
- The configured `EXPIRY_DAYS` value will have no practical effect.
- The system will not satisfy the auto-expiry requirement in `spec_001`.
- Future changes may rely on an `Expired` state that never occurs in production.
- The absence of integration and E2E coverage will leave the full lifecycle unverified.

### 2.8 Benefits of Implementing the Remediation

- Pending requests will follow the complete lifecycle defined by the domain and specification.
- Reserved days will be released automatically when the timeout is reached.
- Employees will recover balance that is no longer tied to an active request.
- Approvers will not retain stale requests indefinitely.
- Audit history will record the automatic transition.
- The system will enforce the decision to use expiry instead of escalation.
- Configuration will become operational, validated, and observable.
- Integration and E2E tests will protect the behavior against regressions.

### 2.9 Conclusion

Auto-expiry is a genuine missing business capability, not merely a missing test. The domain already expresses the intended state transition, but the application has no process that performs it. The remediation should be scheduled after the presentation test suite is stabilized, because deterministic integration tests are required to validate the background process, balance release, audit behavior, and idempotency reliably.

## 3. Security Finding: CDN Scripts Without Subresource Integrity

### 3.1 Current State

`Views/Shared/_ValidationScriptsPartial.cshtml` loads the following scripts from `cdn.jsdelivr.net`:

- jQuery 3.6.0.
- jquery-validation 1.19.5.
- jquery-validation-unobtrusive 4.0.0.

The script tags do not include an `integrity` attribute or a corresponding `crossorigin` policy. The browser therefore has no cryptographic mechanism to verify that the files delivered by the CDN match the versions approved by the project.

The project is currently a development application and is not described as deployed to production. Even so, this is an unresolved security requirement and must be addressed before production deployment or an environment where untrusted external responses could affect users.

### 3.2 Identified Root Cause

The validation scripts were added as external CDN references, while other assets such as Bootstrap are already hosted locally. The implementation did not complete the required asset-governance decision:

- Either host the scripts locally under `wwwroot/lib/`.
- Or use the CDN with approved CSP configuration and Subresource Integrity.

The current state uses the CDN without SRI, leaving the supply-chain protection incomplete.

### 3.3 Security Impact

Every CDN script executes in the browser of a user who loads a page that includes the validation partial. If the CDN, an upstream package, or the delivery path is compromised, modified JavaScript could be executed as part of the application.

Potential consequences include:

- Reading or modifying form data before submission.
- Capturing authentication or vacation-request information entered into the page.
- Altering client-side validation behavior.
- Redirecting users or modifying page content.
- Acting as a starting point for broader client-side attacks if other browser protections are also missing.

SRI does not replace server-side validation, antiforgery protection, or a strong Content Security Policy. It adds an independent integrity check that prevents the browser from accepting a changed file under the expected resource URL.

### 3.4 Specification Reference

This finding is documented in `docs/Keep-in-mind.md` as the first security finding under the `Security findings` section.

The relevant project requirements are:

- Constitution section 7.2: CDN use requires approved CSP configuration and Subresource Integrity where supported.
- `spec_004` checklist: no CDN references should remain in `.cshtml`; assets should be served from `wwwroot/lib/` or `wwwroot/css/`.
- The current `_ValidationScriptsPartial.cshtml` violates that checklist by referencing three external CDN scripts without SRI.

Reference: [spec_004 authentication tasks](../.specify/specs/004-initial-setup-and-authentication/tasks.md).

### 3.5 Recommended Remediation

The preferred remediation is to download and serve the three approved library versions from `wwwroot/lib/`, then remove the CDN references from the Razor partial.

The implementation should also:

- Confirm the exact library versions approved by the project.
- Verify the local files are included in the application build and deployment output.
- Ensure no other `.cshtml` file references an unapproved external script.
- Add a security test or static validation that detects CDN references in views.
- Define the CSP after the asset-hosting decision is complete.

If local hosting is rejected, every supported CDN script must use a verified SRI hash and an appropriate `crossorigin` value, with the CDN origin explicitly covered by the approved CSP. Local hosting remains preferable because it removes runtime dependence on a third-party script delivery service.

### 3.6 Remediation Necessity

**Recommended necessity: 90%.**

This remediation is highly necessary before production deployment because it addresses a supply-chain risk in code executed by every affected browser session. The percentage is below 100% only because the current application is described as a development environment and is not currently deployed as a production service.

The requirement should nevertheless be treated as release-blocking for any production or externally accessible deployment. The fix is small and localized, while the risk of leaving externally controlled JavaScript without integrity verification is avoidable.

### 3.7 Consequences of Not Implementing the Remediation

- The application will continue executing third-party JavaScript without verifying its content.
- A compromised CDN response could affect every user who loads the validation scripts.
- The project will remain non-compliant with its documented CDN and SRI requirements.
- A strict future CSP may block the scripts unexpectedly, causing client-side validation failures.
- Security reviews will continue to report an unresolved supply-chain weakness.
- Production deployment will require an urgent asset and CSP change instead of a controlled, tested change.
- The lack of local asset verification will make deployment behavior depend on external CDN availability.

### 3.8 Benefits of Implementing the Remediation

- The browser will load approved assets from a controlled local location, or verify CDN assets cryptographically through SRI.
- The attack surface associated with third-party script delivery will be reduced.
- The application will align with Constitution section 7.2 and the `spec_004` checklist.
- Future CSP configuration will be simpler and more predictable when assets are local.
- Application startup and page rendering will no longer depend on CDN availability when local hosting is selected.
- Security reviews and deployment readiness checks will have a clear, testable control.
- The change remains isolated to static assets and presentation configuration without altering business logic.

### 3.9 Conclusion

The CDN issue does not normally prevent the application from starting or completing its server-side workflows, but it leaves browser-executed code without the integrity protection required by the project security baseline. The preferred solution is to host the validation libraries locally, verify that no unauthorized CDN references remain, and then define the CSP around the controlled asset set.

This point should be resolved before production exposure. It can be addressed independently after the presentation test suite is stabilized, while its security-header relationship should be considered before implementing the broader headers finding.

## 4. Quality or Verification Debt: E2E Test Suite

### 4.1 Current State

The project references Microsoft Playwright and contains E2E test code, but the E2E suite does not currently provide complete or reliable coverage of the critical browser journeys.

The current state includes:

- `SubmitRequestE2ETests.cs` is a skipped placeholder rather than a complete browser test.
- The browser installation required by Playwright has not been completed and verified in this environment.
- The critical employee request submission journey is not executed end to end.
- Several E2E scenarios listed in `spec_001` and `spec_004` remain unwritten or unverified.
- Existing Role Switcher tests depend on a running application and browser setup, but there is no recorded successful suite execution against a controlled environment.

The suite is therefore partially present but operationally incomplete. This is more precise than saying that Playwright is absent: the dependency exists, while the required execution and coverage are incomplete.

### 4.2 Identified Root Cause

The project created the Playwright test project and some test scaffolding, but browser installation, application startup coordination, test data preparation, and complete scenario implementation were not finished as part of the current work.

The submission test is explicitly marked as skipped because the browser is not installed. In addition, the remaining E2E work was left as future tasks instead of being completed as part of the feature implementation.

### 4.3 Verification Impact

The missing E2E coverage prevents validation of the application from the user's actual browser perspective.

Without operational E2E tests, the project cannot reliably verify:

- That a user can log in through the real browser interface.
- That forms, client-side validation, antiforgery tokens, redirects, and server-side handlers work together.
- That a user can submit a vacation request and see the resulting state.
- That approval, rejection, cancellation, editing, and expiry are visible through the intended UI.
- That navigation, role switching, and authorization behave correctly when accessed through real browser requests.
- That changes to Razor views, JavaScript, CSS, routes, or middleware have not broken critical journeys.

This weakens the project's ability to detect defects that unit and HTTP integration tests cannot reveal, particularly defects involving browser behavior, rendered markup, JavaScript, navigation, and client-server coordination.

### 4.4 Specification Reference

This issue is documented in `docs/Keep-in-mind.md` as the first item under **Quality and verification debt**.

The relevant planned work includes:

- `T206`: browser-based validation of critical application behavior.
- `T304`: E2E coverage for the approval workflow.
- `T403`: E2E coverage for request voiding.
- `T502`: E2E coverage for request history.
- `T601b`: E2E coverage for editing a pending request.
- `T614`: E2E coverage for automatic expiry.
- `T646`: E2E coverage for the approver dashboard.

The Constitution requires Playwright or an approved equivalent for critical browser journeys. The E2E project also references `Microsoft.Playwright`, confirming that the selected testing technology is already part of the intended architecture.

Reference: [spec_001 vacation-request tasks](../.specify/specs/001-vacation-request/tasks.md) and [spec_004 authentication tasks](../.specify/specs/004-initial-setup-and-authentication/tasks.md).

### 4.5 Recommended Remediation

The remediation should establish a repeatable E2E execution environment and complete the critical scenarios.

It should include:

- Install and verify the required Playwright browser binaries.
- Define how the application is started before E2E execution.
- Use a clean, deterministic database and seeded accounts for each run.
- Replace the submission placeholder with a real login, navigation, form submission, and result assertion.
- Implement the remaining critical scenarios listed in the specifications.
- Replace fixed or environment-specific URLs with controlled test configuration.
- Ensure tests wait for actual application state rather than relying on arbitrary delays.
- Capture screenshots, traces, or logs when a browser test fails.
- Run the suite against a freshly initialized application and database.
- Add the E2E execution to CI only after it is deterministic locally.

The first implementation milestone should be the employee submission journey, because it validates the basic browser path and provides the foundation for later request lifecycle scenarios.

### 4.6 Remediation Necessity

**Recommended necessity: 85%.**

This remediation is highly necessary because the Constitution explicitly requires automated browser coverage for critical journeys, and the current suite cannot validate the application as a user experiences it.

The percentage is below 100% because the application can still be exercised manually and some unit or integration tests provide partial coverage. Nevertheless, manual validation is not a reliable substitute for repeatable browser tests, especially after changes to views, JavaScript, routing, or authentication flows.

### 4.7 Consequences of Not Implementing the Remediation

- Browser-specific defects may reach users undetected.
- Broken forms, redirects, routes, JavaScript, or rendered views may pass lower-level tests.
- The project will not fully satisfy its E2E testing requirement.
- Critical journeys will depend on manual verification and individual tester knowledge.
- Regressions in login, request submission, approval, and navigation may be discovered late.
- Auto-expiry, history, and approver workflows will remain unverified from the user interface.
- CI will not validate the complete application journey from browser to database.

### 4.8 Benefits of Implementing the Remediation

- Critical user journeys will be validated through the real browser interface.
- Browser, Razor, JavaScript, routing, middleware, and server behavior will be tested together.
- Regressions will be detected earlier and with more reproducible evidence.
- The project will reduce dependence on manual acceptance testing.
- Screenshots and traces will make UI failures easier to diagnose.
- Authentication and authorization behavior will be validated from the user's perspective.
- Future changes to request workflows will have an automated end-to-end safety net.
- The project will move closer to meeting the Constitution's testing requirements.

### 4.9 Conclusion

The E2E suite is not entirely absent, but it is not currently an operational verification layer. Playwright is referenced and some scenarios exist, yet the critical submission flow remains a skipped placeholder and browser execution has not been established as a repeatable process.

The remediation should begin after the presentation integration tests are stabilized and should start with the request submission journey. Once the browser environment, startup process, test data, and execution evidence are reliable, the remaining critical E2E scenarios can be implemented progressively.

## 5. Decision: Do Not Benchmark on the Docker Stack

### 5.1 Current Decision

Performance benchmarks required by the project must not be performed against the current Docker development stack when SQL Server is running under Apple Silicon emulation.

This is an established technical decision, not an unresolved defect. The Docker stack remains valid for development, functional testing, and local integration work; it is not a valid performance-measurement environment for the project's production targets.

### 5.2 Technical Rationale

The SQL Server image used by the stack publishes an `amd64` image. On Apple Silicon, it runs through Rosetta or equivalent architecture emulation.

That emulation introduces measurable overhead in database operations. A benchmark collected in this environment would combine application performance with emulation cost and would not represent the performance of a native or production-equivalent deployment.

The result could be misleading in both directions:

- The system could appear slower than it would be in a valid environment.
- A non-representative test setup could lead to incorrect capacity or optimization decisions.

### 5.3 Project Impact

The main impact is methodological. If the team measures p95 latency, throughput, or concurrency using the Docker development stack, those measurements cannot be used as reliable evidence for the Constitution's performance targets.

This affects decisions about:

- Login response times.
- Vacation-request operations.
- List and dashboard queries.
- Database capacity.
- RPS and concurrency targets.
- Production release readiness.

The decision does not prevent functional development. It only limits which environment may be used to make performance claims.

### 5.4 Reference and Constraint

The constraint is documented in `docs/Keep-in-mind.md`, the Docker quickstart, the Docker research notes, and Constitution section 12.1.

The project performance targets include:

- Focused operations with p95 below 300 ms.
- Standard MVC pages with p95 below 500 ms.
- Documented RPS and concurrency targets for production releases.

Those targets require a representative measurement environment. The current emulated Docker stack must therefore be excluded from official benchmarking.

### 5.5 Recommended Application

The team should:

- Use Docker for build, functional testing, integration testing, and development.
- Define a native or production-equivalent environment for performance measurements.
- Document the CPU architecture, database engine, storage, memory, and network characteristics of that environment.
- Measure p95, p99, throughput, concurrency, and error rates there.
- Record the environment and workload with every benchmark result.
- Avoid presenting Docker development measurements as production performance evidence.

If performance testing is required before production infrastructure exists, use a native SQL Server environment or another controlled environment with documented differences and limitations.

### 5.6 Necessity of Respecting the Decision

**Recommended necessity: 100%.**

Respecting this decision is mandatory whenever performance results are used for capacity planning, optimization, or release approval. The immediate remediation effort is not to change the application, but to prevent invalid measurements and establish a valid benchmark environment when performance testing begins.

### 5.7 Consequences of Ignoring the Decision

- Performance results may be attributed incorrectly to the application.
- The team may optimize code to compensate for emulation overhead.
- Production capacity may be overestimated or underestimated.
- Release decisions may be based on non-representative p95 and RPS values.
- Database bottlenecks may be confused with architecture-emulation overhead.
- Comparisons between releases may become invalid if the development environment changes.

### 5.8 Benefits of Respecting the Decision

- Performance results will be more representative and defensible.
- Optimization work will target actual application bottlenecks.
- Capacity planning will use meaningful measurements.
- Release performance claims will include their environment and limitations.
- Docker remains useful for development without being misused as a benchmark platform.
- The project avoids creating false confidence from numbers that cannot be reproduced in production.

### 5.9 Conclusion

The Docker stack is appropriate for local development and functional validation, but it is not appropriate for official performance benchmarking while SQL Server runs under architecture emulation. This decision should be preserved as an engineering constraint and revisited only when a native or production-equivalent measurement environment is available.

