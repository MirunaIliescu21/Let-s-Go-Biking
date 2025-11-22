package com.soc;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.node.ObjectNode;
import org.jxmapviewer.viewer.GeoPosition;

import java.io.IOException;
import java.net.URI;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.List;

/**
 * Client REST simplu pentru endpoint-ul /itinerary din RoutingServiceREST.
 */
public class RoutingRestClient {

    // URL-ul tău de la Postman
    private static final String ITINERARY_URL =
            "http://localhost:8733/Design_Time_Addresses/RoutingServiceREST/Service1/itinerary";

    private static final ObjectMapper MAPPER = new ObjectMapper();
    private static final HttpClient CLIENT = HttpClient.newHttpClient();

    /**
     * Trimite un POST cu Origin/Destination și extrage o listă de GeoPosition
     * care conține toată ruta (Walk1 + Bike + Walk2).
     */
    public static List<GeoPosition> fetchRoute(String origin, String destination)
            throws IOException, InterruptedException {

        // Construim body-ul JSON cu Jackson (fără să ne batem cu escape manual)
        ObjectNode req = MAPPER.createObjectNode();
        req.put("Origin", origin);
        req.put("Destination", destination);
        req.put("Debug", true);

        String bodyJson = MAPPER.writeValueAsString(req);

        HttpRequest request = HttpRequest.newBuilder()
                .uri(URI.create(ITINERARY_URL))
                .header("Content-Type", "application/json")
                .POST(HttpRequest.BodyPublishers.ofString(bodyJson, StandardCharsets.UTF_8))
                .build();

        HttpResponse<String> response = CLIENT.send(request, HttpResponse.BodyHandlers.ofString());

        if (response.statusCode() != 200) {
            throw new RuntimeException("REST call failed: " + response.statusCode()
                    + " body=" + response.body());
        }

        JsonNode root = MAPPER.readTree(response.body());

        List<GeoPosition> points = new ArrayList<>();

        // Folosim direct câmpurile din JSON-ul tău: Walk1Coords, BikeCoords, Walk2Coords
        appendCoordsArray(root.get("Walk1Coords"), points);
        appendCoordsArray(root.get("BikeCoords"), points);
        appendCoordsArray(root.get("Walk2Coords"), points);

        // Fallback: dacă pentru un caz nu ai coordonatele segmentelor populate,
        // măcar desenăm linie simplă Origin -> Destination folosind *ResolvedLat/Lon*
        if (points.isEmpty()) {
            double oLat = root.path("OriginResolvedLat").asDouble();
            double oLon = root.path("OriginResolvedLon").asDouble();
            double dLat = root.path("DestResolvedLat").asDouble();
            double dLon = root.path("DestResolvedLon").asDouble();

            points.add(new GeoPosition(oLat, oLon));
            points.add(new GeoPosition(dLat, dLon));
        }

        return points;
    }

    /**
     * Adaugă într-o listă de GeoPosition un array de coordonate de forma
     * [[lat, lon], [lat, lon], ...]
     */
    private static void appendCoordsArray(JsonNode arr, List<GeoPosition> target) {
        if (arr == null || !arr.isArray()) {
            return;
        }
        for (JsonNode pair : arr) {
            if (pair.isArray() && pair.size() >= 2) {
                double lat = pair.get(0).asDouble();
                double lon = pair.get(1).asDouble();
                target.add(new GeoPosition(lat, lon));
            }
        }
    }
}
