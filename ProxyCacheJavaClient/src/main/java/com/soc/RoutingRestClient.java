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

public class RoutingRestClient {

    private static final String ITINERARY_URL =
            "http://localhost:8733/Design_Time_Addresses/RoutingServiceREST/Service1/itinerary";

    private static final ObjectMapper MAPPER = new ObjectMapper();
    private static final HttpClient CLIENT = HttpClient.newHttpClient();

    /**
     * In the initial version we printed the route in a single color
     * without distinguishing between walking and cycling.
     * This method was used in the simple RoutePainter.
     */
    public static List<GeoPosition> fetchRoute(String origin, String destination)
            throws IOException, InterruptedException {

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
        appendCoordsArray(root.get("Walk1Coords"), points);
        appendCoordsArray(root.get("BikeCoords"), points);
        appendCoordsArray(root.get("Walk2Coords"), points);

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
     * Return a list of segments, each with Mode = WALK/BIKE and its points.
     * We use the "Segments" field in the JSON directly.
     */
    public static List<RouteSegment> fetchSegments(String origin, String destination)
            throws IOException, InterruptedException {

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
        JsonNode segmentsNode = root.get("Segments");

        List<RouteSegment> segments = new ArrayList<>();

        if (segmentsNode != null && segmentsNode.isArray()) {
            for (JsonNode segNode : segmentsNode) {
                String modeStr = segNode.path("Mode").asText("walk");
                RouteSegment.Mode mode =
                        "bike".equalsIgnoreCase(modeStr)
                                ? RouteSegment.Mode.BIKE
                                : RouteSegment.Mode.WALK;

                JsonNode coords = segNode.get("Coords");
                List<GeoPosition> pts = new ArrayList<>();
                appendCoordsArray(coords, pts);

                if (!pts.isEmpty()) {
                    segments.add(new RouteSegment(mode, pts));
                }
            }
        }

        /* fallback: if we don't have "Segments", we build a unique segment from the simple list */
        if (segments.isEmpty()) {
            List<GeoPosition> all = new ArrayList<>();
            appendCoordsArray(root.get("Walk1Coords"), all);
            appendCoordsArray(root.get("BikeCoords"), all);
            appendCoordsArray(root.get("Walk2Coords"), all);

            if (all.isEmpty()) {
                double oLat = root.path("OriginResolvedLat").asDouble();
                double oLon = root.path("OriginResolvedLon").asDouble();
                double dLat = root.path("DestResolvedLat").asDouble();
                double dLon = root.path("DestResolvedLon").asDouble();

                all.add(new GeoPosition(oLat, oLon));
                all.add(new GeoPosition(dLat, dLon));
            }

            segments.add(new RouteSegment(RouteSegment.Mode.BIKE, all));
        }

        return segments;
    }

    private static void appendCoordsArray(JsonNode arr, List<GeoPosition> target) {
        if (arr == null || !arr.isArray()) return;

        for (JsonNode pair : arr) {
            if (pair.isArray() && pair.size() >= 2) {
                double lat = pair.get(0).asDouble();
                double lon = pair.get(1).asDouble();
                target.add(new GeoPosition(lat, lon));
            }
        }
    }
}
