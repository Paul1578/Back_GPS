-- Only remove records created by the isolated FletFlow Playwright fixtures.
DELETE rp
FROM RoutePositions rp
INNER JOIN Routes r ON r.Id = rp.RouteId
WHERE r.Name LIKE 'E2E QA ruta %';

DELETE FROM Routes
WHERE Name LIKE 'E2E QA ruta %';

DELETE FROM Drivers
WHERE DocumentNumber LIKE 'E2EQA%';

DELETE FROM Vehicles
WHERE Plate LIKE 'E2EQA%';

DELETE FROM Users
WHERE Email LIKE 'fletflow.e2e.admin.%@example.test'
   OR Email LIKE 'fletflow.e2e.user.%@example.test'
   OR Email LIKE 'fletflow.e2e.driver.%@example.test'
   OR Email LIKE 'fletflow.e2e.manager.map.%@example.test';
