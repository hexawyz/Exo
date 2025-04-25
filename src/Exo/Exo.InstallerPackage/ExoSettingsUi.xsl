<xsl:stylesheet version="2.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform" xmlns:wix="http://wixtoolset.org/schemas/v4/wxs" xmlns:util="http://wixtoolset.org/schemas/v4/wxs/util">
    <xsl:output omit-xml-declaration="yes" indent="yes"/>
    <xsl:strip-space elements="*"/>

    <xsl:template match="node()|@*">
        <xsl:copy>
            <xsl:apply-templates select="node()|@*"/>
        </xsl:copy>
    </xsl:template>

    <xsl:template match='wix:Wix/wix:Fragment/wix:DirectoryRef[@Id="INSTALLFOLDER"]/wix:Directory[@Name="Exo.Settings.Ui"]/wix:Component[wix:File[@Source="SourceDir\Exo.Settings.Ui.exe"]]'>
        <xsl:element name="wix:Component">
            <xsl:attribute name="Id">
                <xsl:value-of select="@Id"/>
            </xsl:attribute>
            <xsl:attribute name="Guid">
                <xsl:value-of select="@Guid"/>
            </xsl:attribute>

            <xsl:element name="wix:File">
                <xsl:attribute name="Id">ExoSettingsUiExecutable</xsl:attribute>
                <xsl:attribute name="Source">
                    <xsl:value-of select="wix:File/@Source"/>
                </xsl:attribute>
                <xsl:attribute name="KeyPath">yes</xsl:attribute>
            </xsl:element>

			<xsl:element name="util:EventSource">
				<xsl:attribute name="Name">Ui</xsl:attribute>
				<xsl:attribute name="Log">Exo</xsl:attribute>
				<xsl:attribute name="EventMessageFile">[NETFRAMEWORK40FULLINSTALLROOTDIR64]EventLogMessages.dll</xsl:attribute>
			</xsl:element>

            <xsl:element name="wix:Shortcut">
                <xsl:attribute name="Id">ApplicationStartMenuShortcut</xsl:attribute>
                <xsl:attribute name="Name">Exo</xsl:attribute>
                <xsl:attribute name="Directory">ProgramMenuFolder</xsl:attribute>
                <xsl:attribute name="Icon">Exo.ico</xsl:attribute>
                <xsl:attribute name="WorkingDirectory">
                    <xsl:value-of select="../@Id"/>
                </xsl:attribute>
                <xsl:attribute name="Advertise">yes</xsl:attribute>
            </xsl:element>
        </xsl:element>
    </xsl:template>

</xsl:stylesheet>
