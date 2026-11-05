<#
.SYNOPSIS
Replaces local demo data and generates bookings through the booking API.

.DESCRIPTION
Run from any directory while the project's Docker Compose services are available.
This intentionally deletes all hotel, room, allotment, inventory, booking, and trace data.
#>
[CmdletBinding()]
param(
    [string]$ApiBaseUrl = 'http://localhost:8080'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$sqlPath = Join-Path $PSScriptRoot 'ResetAndSeedDemoData.sql'
$temporarySqlPath = '/tmp/altairis-seed-demo.sql'

Set-Location $repositoryRoot

$postgresContainer = (docker compose ps -q postgres).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($postgresContainer)) {
    throw 'The PostgreSQL Compose container is not running.'
}

$databaseUser = (docker exec $postgresContainer printenv POSTGRES_USER).Trim()
$databaseName = (docker exec $postgresContainer printenv POSTGRES_DB).Trim()
if ($LASTEXITCODE -ne 0 -or
    [string]::IsNullOrWhiteSpace($databaseUser) -or
    [string]::IsNullOrWhiteSpace($databaseName)) {
    throw 'Could not determine the PostgreSQL database name and user from the container.'
}

try {
    docker cp $sqlPath "$($postgresContainer):$temporarySqlPath"
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not copy the demo data SQL script into the PostgreSQL container.'
    }

    docker exec $postgresContainer psql -v ON_ERROR_STOP=1 -U $databaseUser -d $databaseName -f $temporarySqlPath
    if ($LASTEXITCODE -ne 0) {
        throw 'The transactional hotel, room, allotment, and inventory seed failed.'
    }
}
finally {
    docker exec $postgresContainer rm -f $temporarySqlPath | Out-Null
}

docker compose restart api | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw 'Could not restart the API after loading the base demo data.'
}

for ($attempt = 0; $attempt -lt 30; $attempt++) {
    try {
        $null = Invoke-RestMethod -Uri "$ApiBaseUrl/api/Hotel/search?pageSize=1" -Method Get
        break
    }
    catch {
        if ($attempt -eq 29) {
            throw "The API did not become ready after the inventory cache reload: $($_.Exception.Message)"
        }
        Start-Sleep -Seconds 2
    }
}

$databaseTodayText = (docker exec $postgresContainer psql -U $databaseUser -d $databaseName -Atc 'SELECT CURRENT_DATE::text').Trim()
if ($LASTEXITCODE -ne 0) {
    throw 'Could not read the database date used to anchor the booking period.'
}

$today = [DateTime]::ParseExact($databaseTodayText, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture)
$currentMonthStart = $today.AddDays(1 - $today.Day)
$periodStart = $currentMonthStart.AddMonths(-2)
$periodEnd = $currentMonthStart.AddMonths(4).AddDays(-1)
$regularNights = @(1, 2, 2, 3, 3, 4, 5, 7, 10, 14)
$firstNames = @('Sofia', 'Mateo', 'Lucia', 'Hugo', 'Ines', 'Daniel', 'Amara', 'Leo', 'Camila', 'Rafael', 'Maya', 'Oliver', 'Elena', 'Noah', 'Aisha', 'Thomas', 'Valentina', 'Luca', 'Mila', 'Gabriel', 'Nora', 'Adam', 'Clara', 'Samuel')
$lastNames = @('Garcia', 'Martin', 'Sanchez', 'Rossi', 'Dubois', 'Kowalski', 'Silva', 'Novak', 'Muller', 'Costa', 'Brown', 'Moreau', 'Fernandez', 'Bianchi', 'Jansen', 'Haddad')

$hotelResponse = Invoke-RestMethod -Uri "$ApiBaseUrl/api/Hotel/search?pageSize=100" -Method Get
$hotels = @($hotelResponse.items | Where-Object { $_.active -eq $true } | Sort-Object id)
$createdCount = 0
$canceledCount = 0

foreach ($hotel in $hotels) {
    $hotelId = [long]$hotel.id
    $roomResponse = Invoke-RestMethod -Uri "$ApiBaseUrl/api/Rooms/hotel/$hotelId" -Method Get
    $rooms = @($roomResponse | Sort-Object { [long]$_.id })
    if ($rooms.Count -lt 3) {
        throw "Hotel $hotelId has fewer than three room types."
    }

    $roomNumbers = @{}
    for ($index = 0; $index -lt $rooms.Count; $index++) {
        $roomNumbers[[long]$rooms[$index].id] = $index + 1
    }

    for ($bookingNumber = 1; $bookingNumber -le 30; $bookingNumber++) {
        if ($bookingNumber -eq 1) {
            $baseStart = $today
            $stayNights = 2
        }
        elseif ($bookingNumber -eq 2) {
            $baseStart = $today.AddDays(-2)
            $stayNights = 2
        }
        elseif ($bookingNumber -eq 3) {
            $baseStart = $today.AddDays(-2)
            $stayNights = 4
        }
        elseif ($bookingNumber -eq 4) {
            $baseStart = $today.AddDays(2)
            $stayNights = 3
        }
        else {
            $nightIndex = 1 + ((($hotelId * 7) + ($bookingNumber * 3)) % $regularNights.Count)
            $stayNights = $regularNights[$nightIndex - 1]
            $availableStartDays = ($periodEnd - $periodStart).Days - $stayNights + 1
            $offset = (($hotelId * 37) + ($bookingNumber * 19) + ([Math]::Floor($bookingNumber / 4) * 11)) % $availableStartDays
            $baseStart = $periodStart.AddDays([int]$offset)
        }

        $maximumRoomTypes = [Math]::Min(4, $rooms.Count)
        $desiredRoomCount = 1 + ((($hotelId * 3) + $bookingNumber) % $maximumRoomTypes)
        $selectedRoomIds = @()
        $stayStart = $baseStart

        for ($candidateCount = $desiredRoomCount; $candidateCount -ge 1 -and $selectedRoomIds.Count -eq 0; $candidateCount--) {
            $maximumAttempts = if ($bookingNumber -le 4) { 0 } else { 20 }
            for ($attempt = 0; $attempt -le $maximumAttempts; $attempt++) {
                $candidateStart = $baseStart.AddDays($attempt)
                $candidateEnd = $candidateStart.AddDays($stayNights)
                if ($candidateEnd.AddDays(-1) -gt $periodEnd) {
                    break
                }

                $checkInText = $candidateStart.ToString('yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture)
                $checkOutText = $candidateEnd.ToString('yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture)
                $availabilityUri = "$ApiBaseUrl/api/Booking/availability?hotelId=$hotelId&checkIn=$checkInText&checkOut=$checkOutText"
                $availability = Invoke-RestMethod -Uri $availabilityUri -Method Get
                $availableRoomIds = @($availability.rooms | Where-Object { $_.isAvailable } | ForEach-Object { [long]$_.roomId })
                $orderedAvailableRooms = @(
                    $rooms |
                        Sort-Object { ($roomNumbers[[long]$_.id] + ($bookingNumber * 3) + ($hotelId * 5)) % $rooms.Count } |
                        Where-Object { $availableRoomIds -contains [long]$_.id } |
                        Select-Object -First $candidateCount
                )

                if ($orderedAvailableRooms.Count -eq $candidateCount) {
                    $selectedRoomIds = @($orderedAvailableRooms | ForEach-Object { [long]$_.id })
                    $stayStart = $candidateStart
                    break
                }
            }
        }

        if ($selectedRoomIds.Count -eq 0) {
            throw "No available room type found for hotel $hotelId on $($baseStart.ToString('yyyy-MM-dd')) for $stayNights nights."
        }

        $firstName = $firstNames[1 + ((($hotelId * 7) + ($bookingNumber * 3)) % $firstNames.Count) - 1]
        $lastName = $lastNames[1 + ((($hotelId * 5) + ($bookingNumber * 7)) % $lastNames.Count) - 1]
        $guestEmail = "$($firstName.ToLowerInvariant()).$($lastName.ToLowerInvariant())+$hotelId.$bookingNumber@guest.altairis-demo.example"
        $phonePrefix = switch ($hotel.country) {
            'Spain' { '+34 600 42' }
            'Portugal' { '+351 91 420' }
            'Italy' { '+39 333 420' }
            'France' { '+33 6 42' }
            'Germany' { '+49 151 420' }
            'Netherlands' { '+31 6 420' }
            'Czechia' { '+420 601 420' }
            default { '+44 7700 900' }
        }
        $guestPhone = $phonePrefix + ((($hotelId * 73) + ($bookingNumber * 29)) % 10000).ToString('0000')
        $quoteRequest = @{
            hotelId = $hotelId
            checkIn = $stayStart.ToString('yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture)
            checkOut = $stayStart.AddDays($stayNights).ToString('yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture)
            roomIds = @($selectedRoomIds)
        } | ConvertTo-Json -Depth 4 -Compress

        $quote = Invoke-RestMethod -Uri "$ApiBaseUrl/api/Booking/quote" -Method Post -ContentType 'application/json' -Body $quoteRequest
        $confirmRequest = @{
            guestName = "$firstName $lastName"
            guestPhone = $guestPhone
            guestEmail = $guestEmail
        } | ConvertTo-Json -Depth 3 -Compress
        $confirmation = Invoke-RestMethod -Uri "$ApiBaseUrl/api/Booking/quote/$($quote.quoteId)/confirm" -Method Post -ContentType 'application/json' -Body $confirmRequest
        $createdCount++

        if ($bookingNumber -in @(11, 19, 27)) {
            $null = Invoke-RestMethod -Uri "$ApiBaseUrl/api/Booking/$($confirmation.bookingId)/cancel" -Method Post
            $canceledCount++
        }

        if ($createdCount % 25 -eq 0) {
            Write-Host "Created $createdCount bookings through quote/confirm; canceled $canceledCount."
        }
    }
}

Write-Host "Completed booking generation: $createdCount bookings, $canceledCount canceled."
Write-Host "Check-in period: $($periodStart.ToString('yyyy-MM-dd')) through $($periodEnd.ToString('yyyy-MM-dd'))."

docker exec $postgresContainer psql -v ON_ERROR_STOP=1 -U $databaseUser -d $databaseName `
    -c "UPDATE booking SET booking_date = (LEAST(CURRENT_DATE, check_in::date - 1) - floor(random() * 120)::integer)::timestamp + make_interval(hours => floor(random() * 24)::integer, mins => floor(random() * 60)::integer, secs => floor(random() * 60)::integer) WHERE check_in IS NOT NULL;"
if ($LASTEXITCODE -ne 0) {
    throw 'Bookings were created, but randomizing their creation timestamps failed.'
}

docker exec $postgresContainer psql -U $databaseUser -d $databaseName -P pager=off `
    -c "SELECT 'hotel' AS entity,count(*) AS records FROM hotel UNION ALL SELECT 'room',count(*) FROM room UNION ALL SELECT 'allotment',count(*) FROM allotment UNION ALL SELECT 'room_inventory',count(*) FROM room_inventory UNION ALL SELECT 'booking',count(*) FROM booking UNION ALL SELECT 'booked_rooms',count(*) FROM booked_rooms UNION ALL SELECT 'booking_trace',count(*) FROM booking_trace ORDER BY entity;" `
    -c "SELECT to_char(check_in,'YYYY-MM') AS check_in_month,count(*) AS bookings FROM booking GROUP BY 1 ORDER BY 1;" `
    -c "SELECT count(*) FILTER (WHERE check_in::date=CURRENT_DATE) AS starting_today,count(*) FILTER (WHERE check_out::date=CURRENT_DATE) AS ending_today,count(*) FILTER (WHERE check_in<CURRENT_DATE AND check_out>CURRENT_DATE) AS in_progress,count(*) FILTER (WHERE status='booked') AS booked,count(*) FILTER (WHERE status='canceled') AS canceled FROM booking;"
if ($LASTEXITCODE -ne 0) {
    throw 'Booking generation completed, but the final database summary query failed.'
}
