<xsl:stylesheet version="2.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform" xmlns:wix="http://wixtoolset.org/schemas/v4/wxs" xmlns:util="http://wixtoolset.org/schemas/v4/wxs/util">
    <xsl:output omit-xml-declaration="yes" indent="yes"/>
    <xsl:strip-space elements="*"/>

    <xsl:template match="node()|@*">
        <xsl:copy>
            <xsl:apply-templates select="node()|@*"/>
        </xsl:copy>
    </xsl:template>

    <xsl:template match='wix:Wix/wix:Fragment/wix:DirectoryRef[@Id="INSTALLFOLDER"]/wix:Directory[@Name="Exo.Overlay"]'>
        <xsl:element name="wix:Directory">
            <xsl:attribute name="Id">ExoOverlayDirectory</xsl:attribute>
            <xsl:attribute name="Name">
                <xsl:value-of select="@Name"/>
            </xsl:attribute>
            
            <xsl:apply-templates select="node()"/>
        </xsl:element>
    </xsl:template>

    <xsl:template match='wix:Wix/wix:Fragment/wix:DirectoryRef[@Id="INSTALLFOLDER"]/wix:Directory[@Name="Exo.Overlay"]/wix:Component[wix:File[@Source="SourceDir\Exo.Overlay.exe"]]'>
        <xsl:element name="wix:Component">
            <xsl:attribute name="Id">
                <xsl:value-of select="@Id"/>
            </xsl:attribute>
            <xsl:attribute name="Guid">
                <xsl:value-of select="@Guid"/>
            </xsl:attribute>

            <xsl:element name="wix:File">
                <xsl:attribute name="Id">ExoOverlayExecutable</xsl:attribute>
                <xsl:attribute name="Source">
                    <xsl:value-of select="wix:File/@Source"/>
                </xsl:attribute>
                <xsl:attribute name="KeyPath">yes</xsl:attribute>
            </xsl:element>

			<xsl:element name="util:EventSource">
				<xsl:attribute name="Name">Helper</xsl:attribute>
				<xsl:attribute name="Log">Exo</xsl:attribute>
				<xsl:attribute name="EventMessageFile">[NETFRAMEWORK40FULLINSTALLROOTDIR64]EventLogMessages.dll</xsl:attribute>
			</xsl:element>

            <xsl:element name="wix:RegistryValue">
                <xsl:attribute name="Id">OverlayStartup</xsl:attribute>
                <xsl:attribute name="Root">HKLM</xsl:attribute>
                <xsl:attribute name="Action">write</xsl:attribute>
                <xsl:attribute name="Key">Software\Microsoft\Windows\CurrentVersion\Run</xsl:attribute>
                <xsl:attribute name="Name">Exo Overlay</xsl:attribute>
                <xsl:attribute name="Value">[#ExoOverlayExecutable]</xsl:attribute>
                <xsl:attribute name="Type">string</xsl:attribute>
            </xsl:element>
        </xsl:element>
    </xsl:template>

</xsl:stylesheet>
