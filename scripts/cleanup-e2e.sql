-- Only remove records created by the isolated FletFlow Playwright fixtures.
DELETE rp
FROM RoutePositions rp
INNER JOIN Routes r ON r.Id = rp.RouteId
LEFT JOIN Drivers d ON d.Id = r.DriverId
WHERE r.Name LIKE 'E2E QA ruta %'
   OR d.DocumentNumber LIKE 'E2EQA%';

DELETE r
FROM Routes r
LEFT JOIN Drivers d ON d.Id = r.DriverId
WHERE r.Name LIKE 'E2E QA ruta %'
   OR d.DocumentNumber LIKE 'E2EQA%';

DELETE FROM Drivers
WHERE DocumentNumber LIKE 'E2EQA%';

DELETE FROM Vehicles
WHERE Plate LIKE 'E2EQA%';

DELETE FROM Users
WHERE Email LIKE 'fletflow.e2e.admin.%@example.test'
   OR Email LIKE 'fletflow.e2e.user.%@example.test'
   OR Email LIKE 'fletflow.e2e.driver.%@example.test'
   OR Email LIKE 'fletflow.e2e.logistics.matrix.%@example.test'
   OR Email LIKE 'fletflow.e2e.manager.map.%@example.test';
